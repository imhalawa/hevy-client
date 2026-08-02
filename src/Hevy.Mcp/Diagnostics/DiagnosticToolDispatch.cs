using System.Diagnostics;
using ModelContextProtocol.Protocol;

namespace Hevy.Mcp.Diagnostics;

internal static class DiagnosticToolDispatch
{
  internal static async Task<CallToolResult> InvokeAsync(
      Func<CancellationToken, Task<CallToolResult>> action,
      DiagnosticOperationCategory category,
      DiagnosticSink? diagnostics,
      CancellationToken cancellationToken,
      string operationName = "unknown")
  {

    var started = Stopwatch.GetTimestamp();
    var correlationId = Guid.NewGuid();
    try
    {
      var result = await action(cancellationToken);
      var operationEvent = SafeOperationEvent.FromToolResult(
          category,
          Stopwatch.GetElapsedTime(started),
          result,
          correlationId,
          operationName);
      diagnostics?.Write(LogLevelFor(operationEvent.Status), operationEvent);
      return result;
    }
    catch (OperationCanceledException)
    {
      diagnostics?.Write(
          LogLevel.Warning,
          SafeOperationEvent.Cancelled(category, Stopwatch.GetElapsedTime(started), correlationId, operationName));
      throw;
    }
  }

  private static LogLevel LogLevelFor(DiagnosticOperationStatus status) => status switch
  {
    DiagnosticOperationStatus.Succeeded => LogLevel.Information,
    DiagnosticOperationStatus.Rejected or DiagnosticOperationStatus.Cancelled => LogLevel.Warning,
    _ => LogLevel.Error,
  };
}
