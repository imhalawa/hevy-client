using Hevy.Core.Exceptions;
using ModelContextProtocol.Protocol;
using System.Reflection;
using System.Text.Json;

namespace Hevy.Mcp.Tools;

internal static class ToolExceptionFilter
{
  internal static async Task<CallToolResult> ExecuteAsync(Func<Task<CallToolResult>> action)
  {
    try
    {
      return await action();
    }
    catch (Exception exception)
    {
      return FromException(exception);
    }
  }

  internal static CallToolResult FromException(Exception exception) => exception switch
  {
    TargetInvocationException { InnerException: { } inner } => FromException(inner),
    JsonException => Validation("Tool arguments did not match the advertised input schema."),
    OperationCanceledException cancellation when cancellation.CancellationToken.IsCancellationRequested => throw cancellation,
    OperationCanceledException => ToolResults.Error(new ToolError("timeout", "The Hevy API request timed out.", true, NewCorrelationId())),
    HevyException hevy => ToolResults.Error(new ToolError(hevy.Code, hevy.Message, hevy.IsRetryable, NewCorrelationId(), hevy.StatusCode is null ? null : (int)hevy.StatusCode.Value, hevy.RequestId)),
    HevyCommittedReadbackException committed => ToolResults.Error(new ToolError(committed.Code, committed.Message, committed.IsRetryable, NewCorrelationId())),
    HevyOutcomeUnknownException unknown => ToolResults.Error(new ToolError(unknown.Code, unknown.Message, false, NewCorrelationId(), unknown.StatusCode is null ? null : (int)unknown.StatusCode.Value, unknown.RequestId)),
    HevyConflictException conflict => Conflict(conflict.Message),
    ArgumentException argument => Validation(argument.Message),
    _ => Unexpected(),
  };

  internal static CallToolResult Validation(string message) => ToolResults.Error(new ToolError(
      "validation_error", message, false, NewCorrelationId()));

  internal static CallToolResult Unexpected() => ToolResults.Error(new ToolError(
      "unexpected_error",
      "The tool could not complete the request.",
      false,
      NewCorrelationId()));

  internal static CallToolResult Conflict(string message, object? meta = null) => ToolResults.Error(new ToolError(
      "conflict", message, false, NewCorrelationId()), meta);

  private static string NewCorrelationId() => Guid.NewGuid().ToString("N");
}
