using Hevy.Core.Exceptions;
using ModelContextProtocol.Protocol;

namespace Hevy.Mcp.Tools;

internal static class ToolExceptionFilter
{
  internal static async Task<CallToolResult> ExecuteAsync(Func<Task<CallToolResult>> action)
  {
    try
    {
      return await action();
    }
    catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException)
    {
      return ToolResults.Error(new ToolError(
          "timeout",
          "The Hevy API request timed out.",
          true,
          NewCorrelationId()));
    }
    catch (HevyException exception)
    {
      return ToolResults.Error(new ToolError(
          exception.Code,
          exception.Message,
          exception.IsRetryable,
          NewCorrelationId(),
          exception.StatusCode is null ? null : (int)exception.StatusCode.Value,
          exception.RequestId));
    }
    catch (HevyCommittedReadbackException exception)
    {
      return ToolResults.Error(new ToolError(
          exception.Code,
          exception.Message,
          exception.IsRetryable,
          NewCorrelationId()));
    }
    catch (HevyOutcomeUnknownException exception)
    {
      return ToolResults.Error(new ToolError(
          exception.Code,
          exception.Message,
          false,
          NewCorrelationId(),
          exception.StatusCode is null ? null : (int)exception.StatusCode.Value,
          exception.RequestId));
    }
    catch (HevyConflictException exception)
    {
      return Conflict(exception.Message);
    }
    catch (ArgumentException exception)
    {
      return Validation(exception.Message);
    }
    catch (Exception)
    {
      return Unexpected();
    }
  }

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
