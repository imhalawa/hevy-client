using System.Net;
using System.Security.Cryptography;
using Hevy.Client.Errors;

namespace Hevy.Client.Http;

internal sealed class HevyRetryHandler : DelegatingHandler
{
  internal static readonly HttpRequestOptionsKey<bool> RetrySafeMutation = new("HevyRetrySafeMutation");
  internal static readonly HttpRequestOptionsKey<DateTimeOffset> RetryDeadline = new("HevyRetryDeadline");

  private const int MaximumAttempts = 3;
  private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
  private readonly Func<double> jitter;
  private readonly TimeProvider timeProvider;

  public HevyRetryHandler()
      : this(Task.Delay, () => Random.Shared.NextDouble(), TimeProvider.System)
  {
  }

  internal HevyRetryHandler(Func<TimeSpan, CancellationToken, Task> delayAsync, Func<double> jitter, TimeProvider timeProvider)
  {
    ArgumentNullException.ThrowIfNull(delayAsync);
    ArgumentNullException.ThrowIfNull(jitter);
    ArgumentNullException.ThrowIfNull(timeProvider);
    this.delayAsync = delayAsync;
    this.jitter = jitter;
    this.timeProvider = timeProvider;
  }

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    var retryAllowed = request.Method == HttpMethod.Get ||
        (request.Method == HttpMethod.Put && request.Options.TryGetValue(RetrySafeMutation, out var retrySafe) && retrySafe);
    var mutation = request.Method == HttpMethod.Post || request.Method == HttpMethod.Put;
    var template = retryAllowed ? await RetryRequestTemplate.CreateAsync(request, cancellationToken) : null;

    try
    {
      for (var attempt = 0; attempt < MaximumAttempts; attempt++)
      {
        cancellationToken.ThrowIfCancellationRequested();
        using var retryRequest = template?.CreateRequest();
        var attemptRequest = retryRequest ?? request;
        HevyAuthenticationHandler.EnsureSafeTarget(attemptRequest.RequestUri);

        try
        {
          var response = await base.SendAsync(attemptRequest, cancellationToken);
          if (mutation && (int)response.StatusCode >= 500 && !IsTransient(response.StatusCode))
          {
            var statusCode = response.StatusCode;
            var requestId = HevyResponse.SafeRequestId(response);
            response.Dispose();
            throw new HevyOutcomeUnknownException(statusCode, requestId);
          }

          if (!IsTransient(response.StatusCode))
          {
            return response;
          }

          var retryDelay = GetRetryDelay(response, attempt);
          if (retryAllowed && attempt < MaximumAttempts - 1 && FitsWithinDeadline(request, retryDelay))
          {
            response.Dispose();
            await delayAsync(retryDelay, cancellationToken);
            continue;
          }

          if (mutation)
          {
            var statusCode = response.StatusCode;
            var requestId = HevyResponse.SafeRequestId(response);
            response.Dispose();
            throw new HevyOutcomeUnknownException(statusCode, requestId);
          }

          return response;
        }
        catch (HttpRequestException)
        {
          if (retryAllowed && attempt < MaximumAttempts - 1)
          {
            var retryDelay = GetBackoffDelay(attempt);
            if (FitsWithinDeadline(request, retryDelay))
            {
              await delayAsync(retryDelay, cancellationToken);
              continue;
            }
          }

          if (mutation)
          {
            throw new HevyOutcomeUnknownException();
          }

          throw;
        }
      }

      throw new InvalidOperationException("Retry handling exited without a response.");
    }
    finally
    {
      template?.Dispose();
    }
  }

  private bool FitsWithinDeadline(HttpRequestMessage request, TimeSpan delay) =>
      !request.Options.TryGetValue(RetryDeadline, out var deadline) || timeProvider.GetUtcNow() + delay <= deadline;

  private TimeSpan GetRetryDelay(HttpResponseMessage response, int retryNumber)
  {
    var retryAfter = response.Headers.RetryAfter;
    if (retryAfter?.Delta is TimeSpan delta && delta >= TimeSpan.Zero)
    {
      return delta;
    }

    if (retryAfter?.Date is DateTimeOffset date)
    {
      var remaining = date - timeProvider.GetUtcNow();
      return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    return GetBackoffDelay(retryNumber);
  }

  private TimeSpan GetBackoffDelay(int retryNumber)
  {
    var random = jitter();
    if (double.IsNaN(random) || double.IsInfinity(random))
    {
      random = 0;
    }

    var multiplier = 1d + Math.Clamp(random, 0d, 1d);
    return TimeSpan.FromSeconds(Math.Pow(2d, retryNumber) * multiplier);
  }

  private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
      HttpStatusCode.TooManyRequests or
      HttpStatusCode.InternalServerError or
      HttpStatusCode.BadGateway or
      HttpStatusCode.ServiceUnavailable or
      HttpStatusCode.GatewayTimeout;

  private sealed class RetryRequestTemplate : IDisposable
  {
    private readonly HttpMethod method;
    private readonly Uri? requestUri;
    private readonly Version version;
    private readonly HttpVersionPolicy versionPolicy;
    private readonly IReadOnlyList<KeyValuePair<string, string[]>> headers;
    private readonly IReadOnlyList<KeyValuePair<string, string[]>> contentHeaders;
    private byte[]? content;
    private readonly bool retrySafeMutation;
    private readonly DateTimeOffset? deadline;

    private RetryRequestTemplate(HttpRequestMessage request, byte[]? content)
    {
      method = request.Method;
      requestUri = request.RequestUri;
      version = request.Version;
      versionPolicy = request.VersionPolicy;
      headers = request.Headers.Select(header => new KeyValuePair<string, string[]>(header.Key, header.Value.ToArray())).ToArray();
      contentHeaders = request.Content?.Headers.Select(header => new KeyValuePair<string, string[]>(header.Key, header.Value.ToArray())).ToArray() ?? [];
      this.content = content;
      retrySafeMutation = request.Options.TryGetValue(RetrySafeMutation, out var safe) && safe;
      deadline = request.Options.TryGetValue(RetryDeadline, out var operationDeadline) ? operationDeadline : null;
    }

    public static async Task<RetryRequestTemplate> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        new(request, request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken));

    public HttpRequestMessage CreateRequest()
    {
      var request = new HttpRequestMessage(method, requestUri)
      {
        Version = version,
        VersionPolicy = versionPolicy,
      };
      foreach (var header in headers)
      {
        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
      }

      if (content is not null)
      {
        request.Content = new ByteArrayContent(content);
        foreach (var header in contentHeaders)
        {
          request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
      }

      if (retrySafeMutation)
      {
        request.Options.Set(RetrySafeMutation, true);
      }

      if (deadline is DateTimeOffset operationDeadline)
      {
        request.Options.Set(RetryDeadline, operationDeadline);
      }

      return request;
    }

    public void Dispose()
    {
      if (content is not null)
      {
        CryptographicOperations.ZeroMemory(content);
        content = null;
      }
    }
  }
}
