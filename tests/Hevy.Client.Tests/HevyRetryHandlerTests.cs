using System.Net;
using System.Net.Http.Headers;
using Hevy.Client.Errors;
using Hevy.Client.Http;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyRetryHandlerTests
{
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

    (response.StatusCode).Should().Be(HttpStatusCode.OK);
    (handler.Requests.Count).Should().Be(2);
    (delays).Should().Equal([TimeSpan.FromSeconds(1)]);
  }

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

    (response.StatusCode).Should().Be(HttpStatusCode.OK);
    (jitterCalls).Should().Be(1);
    (delays).Should().Equal([TimeSpan.FromSeconds(1)]);
  }

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

    (response.StatusCode).Should().Be(HttpStatusCode.OK);
    (handler.Requests.Count).Should().Be(2);
    (delays).Should().Equal([TimeSpan.FromSeconds(7)]);
  }

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

    (response.StatusCode).Should().Be(HttpStatusCode.OK);
    (delays).Should().Equal([TimeSpan.FromSeconds(4)]);
  }

  [Fact]
  public async Task Get_retries_a_503_no_more_than_three_total_attempts()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays);

    using var response = await client.GetAsync("v1/user/info", CancellationToken.None);

    (response.StatusCode).Should().Be(HttpStatusCode.ServiceUnavailable);
    (handler.Requests.Count).Should().Be(3);
    (delays).Should().Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)]);
  }

  [Fact]
  public async Task Get_does_not_retry_an_unselected_5xx_response()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.NotImplemented, "{}"));
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays);

    using var response = await client.GetAsync("v1/user/info", CancellationToken.None);

    (response.StatusCode).Should().Be(HttpStatusCode.NotImplemented);
    (handler.Requests).Should().ContainSingle();
    (delays).Should().BeEmpty();
  }

  [Fact]
  public async Task Post_maps_an_unselected_5xx_response_to_an_unknown_outcome_without_retrying()
  {
    var handler = new RecordingHttpMessageHandler((_, _) =>
    {
      var response = RecordingHttpMessageHandler.Json(HttpStatusCode.NotImplemented, "{}");
      response.Headers.Add("X-Request-Id", "safe-request-id");
      return response;
    });
    using var client = CreateClient(handler, []);
    using var request = JsonPost("v1/workouts");

    var exception = (await FluentActions.Awaiting(() => client.SendAsync(request, CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>()).Which;

    (exception.StatusCode).Should().Be(HttpStatusCode.NotImplemented);
    (exception.RequestId).Should().Be("safe-request-id");
    (handler.Requests).Should().ContainSingle();
  }

  [Fact]
  public async Task Post_does_not_retry_and_reports_an_unknown_outcome_after_a_transient_response()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
    var delays = new List<TimeSpan>();
    using var client = CreateClient(handler, delays);
    using var request = JsonPost("v1/workouts");

    var exception = (await FluentActions.Awaiting(() => client.SendAsync(request, CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>()).Which;

    (exception.Code).Should().Be("outcome_unknown");
    (handler.Requests).Should().ContainSingle();
    (delays).Should().BeEmpty();
  }

  [Fact]
  public async Task Put_retries_only_when_explicitly_marked_safe()
  {
    var unsafeHandler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
    using var unsafeClient = CreateClient(unsafeHandler, []);
    using var unsafeRequest = JsonPut("v1/workouts/workout-1");

    await FluentActions.Awaiting(() => unsafeClient.SendAsync(unsafeRequest, CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>();

    var responses = new Queue<HttpResponseMessage>([
        RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}")]);
    var safeHandler = new RecordingHttpMessageHandler((_, _) => responses.Dequeue());
    var delays = new List<TimeSpan>();
    using var safeClient = CreateClient(safeHandler, delays);
    using var safeRequest = JsonPut("v1/workouts/workout-1");
    safeRequest.Options.Set(HevyRetryHandler.RetrySafeMutation, true);

    using var response = await safeClient.SendAsync(safeRequest, CancellationToken.None);

    (unsafeHandler.Requests).Should().ContainSingle();
    (response.StatusCode).Should().Be(HttpStatusCode.OK);
    (safeHandler.Requests.Count).Should().Be(2);
    (delays).Should().Equal([TimeSpan.FromSeconds(1)]);
  }

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

    (response.StatusCode).Should().Be(HttpStatusCode.OK);
    (requests.Count).Should().Be(2);
    (requests[1]).Should().NotBeSameAs(requests[0]);
    (handler.Requests).Should().AllSatisfy(sent => (sent.Body).Should().Be("{\"title\":\"sanitized\"}"));
  }

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

    (response.StatusCode).Should().Be(HttpStatusCode.TooManyRequests);
    (handler.Requests).Should().ContainSingle();
    (delays).Should().BeEmpty();
  }

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

    await FluentActions.Awaiting(() => client.GetAsync("v1/user/info", cancellation.Token)).Should().ThrowAsync<OperationCanceledException>();

    (handler.Requests).Should().ContainSingle();
  }

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

    (response.StatusCode).Should().Be(HttpStatusCode.OK);
    (handler.Requests.Count).Should().Be(2);
    (handler.Requests).Should().AllSatisfy(sent => (sent.RequestUri!.AbsoluteUri).Should().Be("https://api.hevyapp.com/v1/user/info"));
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
