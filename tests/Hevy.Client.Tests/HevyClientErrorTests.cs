using System.Net;
using Hevy.Client;
using Hevy.Client.Errors;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyClientErrorTests
{
  // Break caught: upstream status failures losing their stable local category or leaking a sensitive response body.
  [Theory]
  [InlineData(HttpStatusCode.Unauthorized, "authentication", false)]
  [InlineData(HttpStatusCode.Forbidden, "authorization", false)]
  [InlineData(HttpStatusCode.NotFound, "not_found", false)]
  [InlineData(HttpStatusCode.Conflict, "conflict", false)]
  [InlineData(HttpStatusCode.TooManyRequests, "rate_limited", true)]
  [InlineData(HttpStatusCode.InternalServerError, "transient_upstream", true)]
  public async Task Failed_responses_become_safe_stable_exceptions(HttpStatusCode statusCode, string code, bool retryable)
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(statusCode, "{\"detail\":\"response-secret\"}"));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = await Assert.ThrowsAsync<HevyException>(() => client.GetUserInfoAsync(CancellationToken.None));

    Assert.Equal(code, exception.Code);
    Assert.Equal(retryable, exception.IsRetryable);
    Assert.Equal(statusCode, exception.StatusCode);
    Assert.DoesNotContain("response-secret", exception.Message, StringComparison.Ordinal);
    Assert.DoesNotContain("api-key-secret", exception.ToString(), StringComparison.Ordinal);
  }

  // Break caught: an empty or malformed success response appearing as a successful null result.
  [Theory]
  [InlineData("")]
  [InlineData("{")]
  public async Task Invalid_success_responses_become_safe_unexpected_response_errors(string response)
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = await Assert.ThrowsAsync<HevyException>(() => client.GetUserInfoAsync(CancellationToken.None));

    Assert.Equal("unexpected_response", exception.Code);
    Assert.False(exception.IsRetryable);
    Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
    Assert.DoesNotContain("api-key-secret", exception.ToString(), StringComparison.Ordinal);
  }

  // Break caught: a transport exception's untrusted text being retained as an inner exception and exposed by ToString().
  [Fact]
  public async Task Transport_failure_does_not_retain_sensitive_inner_exception_text()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => throw new HttpRequestException("transport-secret"));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = await Assert.ThrowsAsync<HevyException>(() => client.GetUserInfoAsync(CancellationToken.None));

    Assert.DoesNotContain("transport-secret", exception.Message, StringComparison.Ordinal);
    Assert.DoesNotContain("transport-secret", exception.ToString(), StringComparison.Ordinal);
    Assert.Null(exception.InnerException);
  }

  // Break caught: malformed response content being retained through a JsonException at the public error boundary.
  [Fact]
  public async Task Malformed_response_does_not_retain_sensitive_payload_details()
  {
    const string sensitivePayload = "{\"name\":\"malformed-payload-secret\"";
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, sensitivePayload));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = await Assert.ThrowsAsync<HevyException>(() => client.GetUserInfoAsync(CancellationToken.None));

    Assert.DoesNotContain("malformed-payload-secret", exception.Message, StringComparison.Ordinal);
    Assert.DoesNotContain("malformed-payload-secret", exception.ToString(), StringComparison.Ordinal);
    Assert.Null(exception.InnerException);
  }

  // Break caught: cancellation being translated to an API failure or ignored by the HTTP request.
  [Fact]
  public async Task Cancellation_is_propagated_without_normalization()
  {
    var handler = new RecordingHttpMessageHandler((_, cancellationToken) =>
    {
      cancellationToken.ThrowIfCancellationRequested();
      return RecordingHttpMessageHandler.Json(HttpStatusCode.OK, Fixture.Read("user-info.json"));
    });
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("test-api-key"));
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetUserInfoAsync(cancellation.Token));

    Assert.True(Assert.Single(handler.Requests).CancellationToken.IsCancellationRequested);
  }
}
