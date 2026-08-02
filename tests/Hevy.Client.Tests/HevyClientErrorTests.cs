using System.Net;
using System.Text;
using Hevy.Client.Http;
using Hevy.Core.Exceptions;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyClientErrorTests
{
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

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be(code);
    (exception.IsRetryable).Should().Be(retryable);
    (exception.StatusCode).Should().Be(statusCode);
    (exception.Message).Should().NotContain("response-secret");
    (exception.ToString()).Should().NotContain("api-key-secret");
  }

  [Fact]
  public async Task Failed_responses_preserve_only_the_safe_upstream_request_identifier()
  {
    var handler = new RecordingHttpMessageHandler((_, _) =>
    {
      var response = RecordingHttpMessageHandler.Json(HttpStatusCode.BadRequest, "{}");
      response.Headers.TryAddWithoutValidation("X-Request-Id", "hevy-request-123");
      return response;
    });
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(default)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.RequestId).Should().Be("hevy-request-123");
  }

  [Theory]
  [InlineData("")]
  [InlineData("{")]
  public async Task Invalid_success_responses_become_safe_unexpected_response_errors(string response)
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
    (exception.IsRetryable).Should().BeFalse();
    (exception.StatusCode).Should().Be(HttpStatusCode.OK);
    (exception.ToString()).Should().NotContain("api-key-secret");
  }

  [Fact]
  public async Task Transport_failure_does_not_retain_sensitive_inner_exception_text()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => throw new HttpRequestException("transport-secret"));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Message).Should().NotContain("transport-secret");
    (exception.ToString()).Should().NotContain("transport-secret");
    (exception.InnerException).Should().BeNull();
  }

  [Fact]
  public async Task Malformed_response_does_not_retain_sensitive_payload_details()
  {
    const string sensitivePayload = "{\"name\":\"malformed-payload-secret\"";
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, sensitivePayload));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Message).Should().NotContain("malformed-payload-secret");
    (exception.ToString()).Should().NotContain("malformed-payload-secret");
    (exception.InnerException).Should().BeNull();
  }

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

    await FluentActions.Awaiting(() => client.GetUserInfoAsync(cancellation.Token)).Should().ThrowAsync<OperationCanceledException>();

    ((handler.Requests).Should().ContainSingle().Which.CancellationToken.IsCancellationRequested).Should().BeTrue();
  }

  [Fact]
  public async Task Http_client_timeout_on_a_read_becomes_a_safe_retryable_timeout()
  {
    using var httpClient = new HttpClient(new DelayingHandler()) { Timeout = TimeSpan.FromMilliseconds(20) };
    var client = new HevyClient(httpClient, new HevyClientOptions("api-key-secret"));

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("timeout");
    (exception.IsRetryable).Should().BeTrue();
    (exception.ToString()).Should().NotContain("api-key-secret");
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("{\"data\":null}")]
  [InlineData("{\"data\":{\"id\":null,\"name\":\"User\",\"url\":\"https://example.invalid\"}}")]
  public async Task Missing_required_response_members_are_rejected(string response)
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
    (exception.IsRetryable).Should().BeFalse();
  }

  [Fact]
  public async Task Oversized_ordinary_response_is_rejected_at_the_byte_ceiling()
  {
    var response = "{\"data\":{\"id\":\"user-1\",\"name\":\"User\",\"url\":\"https://example.invalid\"},\"extra\":\"" + new string('x', 4_194_304) + "\"}";
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
  }

  [Fact]
  public async Task Oversized_response_without_a_content_length_is_rejected_at_the_byte_ceiling()
  {
    var payload = Encoding.UTF8.GetBytes(new string('x', HevyResponse.MaximumResponseBytes + 1));
    var handler = new RecordingHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StreamContent(new MemoryStream(payload)),
    });
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("api-key-secret"));

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
  }

  private sealed class DelayingHandler : HttpMessageHandler
  {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      throw new InvalidOperationException("Unreachable.");
    }
  }
}
