using Hevy.Mcp.Tools;
using Hevy.Client.Errors;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class ToolExceptionFilterTests
{
  [Fact]
  public async Task Internal_operation_cancellation_becomes_a_safe_timeout_error()
  {
    var result = await ToolExceptionFilter.ExecuteAsync(
        () => Task.FromException<ModelContextProtocol.Protocol.CallToolResult>(new TaskCanceledException("transport detail")));

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("timeout");
    (result.Structured().GetProperty("error").GetProperty("retryable").GetBoolean()).Should().BeTrue();
    (result.Structured().GetRawText()).Should().NotContain("transport detail");
  }

  [Fact]
  public async Task Caller_cancellation_still_propagates()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await FluentActions.Awaiting(() => ToolExceptionFilter.ExecuteAsync(
        () => Task.FromCanceled<ModelContextProtocol.Protocol.CallToolResult>(cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();
  }

  [Fact]
  public async Task Outcome_unknown_preserves_only_the_safe_upstream_request_identifier()
  {
    var result = await ToolExceptionFilter.ExecuteAsync(
        () => Task.FromException<ModelContextProtocol.Protocol.CallToolResult>(
            new HevyOutcomeUnknownException(System.Net.HttpStatusCode.ServiceUnavailable, "safe-request-id")));

    var error = result.Structured().GetProperty("error");
    (error.GetProperty("code").GetString()).Should().Be("outcome_unknown");
    (error.GetProperty("hevy_request_id").GetString()).Should().Be("safe-request-id");
  }
}
