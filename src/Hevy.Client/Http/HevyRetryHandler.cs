using System.Net;
using Hevy.Core.Exceptions;
using Polly;
using Polly.Retry;

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
    var mutation = request.Method == HttpMethod.Post || request.Method == HttpMethod.Put;
    using var template = retryAllowed ? await HevyRetryRequestTemplate.CreateAsync(request, cancellationToken) : null;
    var retryDelay = TimeSpan.Zero;

    var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
        .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
          MaxRetryAttempts = 2,
          ShouldHandle = arguments =>
          {
            var hasTransientException = arguments.Outcome.Exception is HttpRequestException;
            var hasTransientResponse = arguments.Outcome.Result is { } response && IsTransient(response.StatusCode);
            if (!retryAllowed || (!hasTransientException && !hasTransientResponse))
            {
              return ValueTask.FromResult(false);
            }

            retryDelay = GetRetryDelay(arguments.Outcome.Result, arguments.AttemptNumber);
            return ValueTask.FromResult(FitsWithinDeadline(request, retryDelay));
          },
          DelayGenerator = _ => ValueTask.FromResult<TimeSpan?>(TimeSpan.Zero),
          OnRetry = async arguments =>
          {
            arguments.Outcome.Result?.Dispose();
            await delayAsync(retryDelay, arguments.Context.CancellationToken).ConfigureAwait(false);
          },
        })
        .Build();

    try
    {
      var response = await pipeline.ExecuteAsync(async token =>
      {
        using var retryRequest = template?.CreateRequest();
        var attemptRequest = retryRequest ?? request;
        HevyAuthenticationHandler.EnsureSafeTarget(attemptRequest.RequestUri);
        return await base.SendAsync(attemptRequest, token).ConfigureAwait(false);
      }, cancellationToken).ConfigureAwait(false);

      if (mutation && (int)response.StatusCode >= 500)
      {
        var statusCode = response.StatusCode;
        var requestId = HevyResponse.SafeRequestId(response);
        response.Dispose();
        throw new HevyOutcomeUnknownException(statusCode, requestId);
      }

      return response;
    }
    catch (HttpRequestException) when (mutation)
    {
      throw new HevyOutcomeUnknownException();
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
