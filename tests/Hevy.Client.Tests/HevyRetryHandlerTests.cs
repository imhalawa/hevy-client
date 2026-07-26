using System.Net;
using System.Net.Http.Headers;
using Hevy.Client.Errors;
using Hevy.Client.Http;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyRetryHandlerTests
{
  // Break caught: a transient connection failure making a read fail immediately instead of retrying the request.
  [Fact]
  public async Task Get_retries_a_connection_error_and_returns_the_later_response()
  {
    var attempts = 0;
    var handler = new RecordingHttpMessageHandler((_, _) => ++attempts == 1
        ? throw new HttpRequestException("transient")
        : RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays);

    using var response = await client.GetAsync("v1/user/info", CancellationToken.None);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal([TimeSpan.FromSeconds(1)], delays);
  }

  // Break caught: sampling jitter separately for the retry-deadline check and the actual connection-error delay.
  [Fact]
  public async Task Connection_retry_samples_jitter_once_per_delay()
  {
    var attempts = 0;
    var handler = new RecordingHttpMessageHandler((_, _) => ++attempts == 1
        ? throw new HttpRequestException("transient")
        : RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
    var delays = new List<TimeSpan>();
    var jitterCalls = 0;
    var retry = new HevyRetryHandler(
        (delay, _) =>
        {
          delays.Add(delay);
          return Task.CompletedTask;
        },
        () =>
        {
          jitterCalls++;
          return 0d;
        },
        TimeProvider.System)
    {
      InnerHandler = handler,
    };
    using var client = new HttpClient(retry) { BaseAddress = HevyAuthenticationHandler.ApiOrigin };

    using var response = await client.GetAsync("v1/user/info", CancellationToken.None);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, jitterCalls);
    Assert.Equal([TimeSpan.FromSeconds(1)], delays);
  }

  // Break caught: rate-limit responses being retried before their server-provided Retry-After interval.
  [Fact]
  public async Task Get_honors_retry_after_before_retrying_a_rate_limited_response()
  {
    var responses = new Queue<HttpResponseMessage>([
        ResponseWithRetryAfter(TimeSpan.FromSeconds(7)),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}")]);
    var handler = new RecordingHttpMessageHandler((_, _) => responses.Dequeue());
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays);

    using var response = await client.GetAsync("v1/user/info", CancellationToken.None);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal([TimeSpan.FromSeconds(7)], delays);
  }

  // Break caught: a Retry-After date being interpreted relative to wall-clock time instead of the injected operation clock.
  [Fact]
  public async Task Get_uses_the_injected_clock_for_a_retry_after_date()
  {
    var now = new DateTimeOffset(2026, 7, 26, 10, 0, 0, TimeSpan.Zero);
    var responses = new Queue<HttpResponseMessage>([
        ResponseWithRetryAfter(now.AddSeconds(4)),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}")]);
    var handler = new RecordingHttpMessageHandler((_, _) => responses.Dequeue());
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays, timeProvider: new FixedTimeProvider(now));

    using var response = await client.GetAsync("v1/user/info", CancellationToken.None);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal([TimeSpan.FromSeconds(4)], delays);
  }

  // Break caught: selected 5xx read failures exceeding the three-attempt ceiling.
  [Fact]
  public async Task Get_retries_a_503_no_more_than_three_total_attempts()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays);

    using var response = await client.GetAsync("v1/user/info", CancellationToken.None);

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Equal(3, handler.Requests.Count);
    Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], delays);
  }

  // Break caught: retrying a non-transient 5xx status that is outside the conservative retry allow-list.
  [Fact]
  public async Task Get_does_not_retry_an_unselected_5xx_response()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.NotImplemented, "{}"));
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays);

    using var response = await client.GetAsync("v1/user/info", CancellationToken.None);

    Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    Assert.Single(handler.Requests);
    Assert.Empty(delays);
  }

  // Break caught: returning an unselected 5xx for a transmitted mutation, which invites the caller to replay an ambiguous write.
  [Fact]
  public async Task Post_maps_an_unselected_5xx_response_to_an_unknown_outcome_without_retrying()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.NotImplemented, "{}"));
    using var client = CreateClient(handler, []);
    using var request = JsonPost("v1/workouts");

    var exception = await Assert.ThrowsAsync<HevyOutcomeUnknownException>(() => client.SendAsync(request, CancellationToken.None));

    Assert.Equal(HttpStatusCode.NotImplemented, exception.StatusCode);
    Assert.Single(handler.Requests);
  }

  // Break caught: a non-idempotent POST being silently replayed after the server may have received its body.
  [Fact]
  public async Task Post_does_not_retry_and_reports_an_unknown_outcome_after_a_transient_response()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays);
    using var request = JsonPost("v1/workouts");

    var exception = await Assert.ThrowsAsync<HevyOutcomeUnknownException>(() => client.SendAsync(request, CancellationToken.None));

    Assert.Equal("outcome_unknown", exception.Code);
    Assert.Single(handler.Requests);
    Assert.Empty(delays);
  }

  // Break caught: PUT retries either occurring without an explicit idempotency mark or being disabled despite that mark.
  [Fact]
  public async Task Put_retries_only_when_explicitly_marked_safe()
  {
    var unsafeHandler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
    using var unsafeClient = CreateClient(unsafeHandler, []);
    using var unsafeRequest = JsonPut("v1/workouts/workout-1");

    await Assert.ThrowsAsync<HevyOutcomeUnknownException>(() => unsafeClient.SendAsync(unsafeRequest, CancellationToken.None));

    var responses = new Queue<HttpResponseMessage>([
        RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}")]);
    var safeHandler = new RecordingHttpMessageHandler((_, _) => responses.Dequeue());
    var delays = new List<TimeSpan>();
    using var safeClient = CreateClient(safeHandler, delays);
    using var safeRequest = JsonPut("v1/workouts/workout-1");
    safeRequest.Options.Set(HevyRetryHandler.RetrySafeMutation, true);

    using var response = await safeClient.SendAsync(safeRequest, CancellationToken.None);

    Assert.Single(unsafeHandler.Requests);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(2, safeHandler.Requests.Count);
    Assert.Equal([TimeSpan.FromSeconds(1)], delays);
  }

  // Break caught: replaying the same HttpRequestMessage and content instance through a lower handler after it has already been sent.
  [Fact]
  public async Task Safe_put_uses_a_fresh_request_for_each_attempt()
  {
    var requests = new List<HttpRequestMessage>();
    var responses = new Queue<HttpResponseMessage>([
        RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}")]);
    var handler = new RecordingHttpMessageHandler((request, _) =>
    {
      requests.Add(request);
      return responses.Dequeue();
    });
    using var client = CreateClient(handler, []);
    using var request = JsonPut("v1/body_measurements/2024-08-14");
    request.Options.Set(HevyRetryHandler.RetrySafeMutation, true);

    using var response = await client.SendAsync(request, CancellationToken.None);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(2, requests.Count);
    Assert.NotSame(requests[0], requests[1]);
    Assert.All(handler.Requests, sent => Assert.Equal("{\"title\":\"sanitized\"}", sent.Body));
  }

  // Break caught: honoring a Retry-After value that exceeds the remaining operation deadline.
  [Fact]
  public async Task Get_does_not_wait_past_the_operation_deadline()
  {
    var now = new DateTimeOffset(2026, 7, 26, 10, 0, 0, TimeSpan.Zero);
    var handler = new RecordingHttpMessageHandler((_, _) => ResponseWithRetryAfter(TimeSpan.FromSeconds(4)));
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays, timeProvider: new FixedTimeProvider(now));
    using var request = new HttpRequestMessage(HttpMethod.Get, "v1/user/info");
    request.Options.Set(HevyRetryHandler.RetryDeadline, now.AddSeconds(3));

    using var response = await client.SendAsync(request, CancellationToken.None);

    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    Assert.Single(handler.Requests);
    Assert.Empty(delays);
  }

  // Break caught: retry backoff continuing after the caller cancels the operation.
  [Fact]
  public async Task Cancellation_during_retry_delay_stops_further_attempts()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
    using var cancellation = new CancellationTokenSource();
    using var client = CreateClient(
        handler,
        [],
        (_, token) =>
        {
          cancellation.Cancel();
          return Task.Delay(Timeout.InfiniteTimeSpan, token);
        });

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("v1/user/info", cancellation.Token));

    Assert.Single(handler.Requests);
  }

  // Break caught: a lower handler changing one sent request's destination and contaminating later retry attempts.
  [Fact]
  public async Task Retry_uses_the_original_exact_hevy_origin_for_every_attempt()
  {
    var attempts = 0;
    var handler = new RecordingHttpMessageHandler((request, _) =>
    {
      attempts++;
      if (attempts == 1)
      {
        request.RequestUri = new Uri("https://untrusted.example/v1/user/info");
        return RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}");
      }

      return RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
    });
    using var client = CreateClient(handler, []);

    using var response = await client.GetAsync("v1/user/info", CancellationToken.None);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(2, handler.Requests.Count);
    Assert.All(handler.Requests, sent => Assert.Equal("https://api.hevyapp.com/v1/user/info", sent.RequestUri!.AbsoluteUri));
  }

  private static HttpClient CreateClient(RecordingHttpMessageHandler handler, List<TimeSpan> delays, Func<TimeSpan, CancellationToken, Task>? delayAsync = null, TimeProvider? timeProvider = null)
  {
    var retry = new HevyRetryHandler(
        delayAsync ?? ((delay, _) =>
        {
          delays.Add(delay);
          return Task.CompletedTask;
        }),
        () => 0d,
        timeProvider ?? TimeProvider.System)
    {
      InnerHandler = handler,
    };
    return new HttpClient(retry) { BaseAddress = HevyAuthenticationHandler.ApiOrigin };
  }

  private static HttpRequestMessage JsonPost(string path) => new(HttpMethod.Post, path)
  {
    Content = new StringContent("{\"title\":\"sanitized\"}"),
  };

  private static HttpRequestMessage JsonPut(string path) => new(HttpMethod.Put, path)
  {
    Content = new StringContent("{\"title\":\"sanitized\"}"),
  };

  private static HttpResponseMessage ResponseWithRetryAfter(TimeSpan delay)
  {
    var response = RecordingHttpMessageHandler.Json(HttpStatusCode.TooManyRequests, "{}");
    response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);
    return response;
  }

  private static HttpResponseMessage ResponseWithRetryAfter(DateTimeOffset date)
  {
    var response = RecordingHttpMessageHandler.Json(HttpStatusCode.TooManyRequests, "{}");
    response.Headers.RetryAfter = new RetryConditionHeaderValue(date);
    return response;
  }

  private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => now;
  }
}
