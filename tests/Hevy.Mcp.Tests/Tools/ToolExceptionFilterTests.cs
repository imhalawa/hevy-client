using Hevy.Mcp.Tools;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class ToolExceptionFilterTests
{
  // Break caught: an uncancelled internal timeout escaping the tool boundary as MCP cancellation.
  [Fact]
  public async Task Internal_operation_cancellation_becomes_a_safe_timeout_error()
  {
    var result = await ToolExceptionFilter.ExecuteAsync(
        () => Task.FromException<ModelContextProtocol.Protocol.CallToolResult>(new TaskCanceledException("transport detail")));

    Assert.True(result.IsError);
    Assert.Equal("timeout", result.Structured().GetProperty("error").GetProperty("code").GetString());
    Assert.True(result.Structured().GetProperty("error").GetProperty("retryable").GetBoolean());
    Assert.DoesNotContain("transport detail", result.Structured().GetRawText(), StringComparison.Ordinal);
  }

  // Break caught: genuine caller cancellation being converted into a tool error envelope.
  [Fact]
  public async Task Caller_cancellation_still_propagates()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToolExceptionFilter.ExecuteAsync(
        () => Task.FromCanceled<ModelContextProtocol.Protocol.CallToolResult>(cancellation.Token)));
  }
}
