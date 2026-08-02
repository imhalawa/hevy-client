using System.Net;

namespace Hevy.Client.Http;

internal sealed class HevyRetryHandler : DelegatingHandler
{
  internal static readonly HttpRequestOptionsKey<bool> RetrySafeMutation = new("HevyRetrySafeMutation");
  internal static readonly HttpRequestOptionsKey<DateTimeOffset> RetryDeadline = new("HevyRetryDeadline");

  private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
  private readonly Func<double> jitter;
  private readonly TimeProvider timeProvider;

  public HevyRetryHandler()
      : this(Task.Delay, Random.Shared.NextDouble, TimeProvider.System)
  {
  }

  internal HevyRetryHandler(Func<TimeSpan, CancellationToken, Task> delayAsync, Func<double> jitter, TimeProvider timeProvider)
  {
    this.delayAsync = delayAsync;
    this.jitter = jitter;
    this.timeProvider = timeProvider;
  }

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var retryAllowed = request.Method == HttpMethod.Get ||
        (request.Method == HttpMethod.Put && request.Options.TryGetValue(RetrySafeMutation, out var retrySafe) && retrySafe);
    if (!retryAllowed)
    {
      HevyAuthenticationHandler.EnsureSafeTarget(request.RequestUri);
      return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    using var template = await HevyRetryRequestTemplate.CreateAsync(request, cancellationToken);
    for (var attempt = 0; ; attempt++)
    {
      using var attemptRequest = template.CreateRequest();
      HevyAuthenticationHandler.EnsureSafeTarget(attemptRequest.RequestUri);
      try
      {
        var response = await base.SendAsync(attemptRequest, cancellationToken).ConfigureAwait(false);
        var delay = GetRetryDelay(response, attempt);
        if (attempt == 2 || !IsTransient(response.StatusCode) || !FitsWithinDeadline(request, delay)) return response;
        response.Dispose();
        await delayAsync(delay, cancellationToken).ConfigureAwait(false);
      }
      catch (HttpRequestException) when (attempt < 2)
      {
        var delay = GetRetryDelay(null, attempt);
        if (!FitsWithinDeadline(request, delay)) throw;
        await delayAsync(delay, cancellationToken).ConfigureAwait(false);
      }
    }
  }

  private bool FitsWithinDeadline(HttpRequestMessage request, TimeSpan delay) =>
      !request.Options.TryGetValue(RetryDeadline, out var deadline) || timeProvider.GetUtcNow() + delay <= deadline;

  private TimeSpan GetRetryDelay(HttpResponseMessage? response, int retryNumber)
  {
    var retryAfter = response?.Headers.RetryAfter;
    if (retryAfter?.Delta is TimeSpan delta && delta >= TimeSpan.Zero) return delta;
    if (retryAfter?.Date is DateTimeOffset date)
    {
      var remaining = date - timeProvider.GetUtcNow();
      return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    var random = jitter();
    if (double.IsNaN(random) || double.IsInfinity(random)) random = 0;
    return TimeSpan.FromSeconds(Math.Pow(2d, retryNumber) * (1d + Math.Clamp(random, 0d, 1d)));
  }

  private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
      HttpStatusCode.TooManyRequests or
      HttpStatusCode.InternalServerError or
      HttpStatusCode.BadGateway or
      HttpStatusCode.ServiceUnavailable or
      HttpStatusCode.GatewayTimeout;
}
