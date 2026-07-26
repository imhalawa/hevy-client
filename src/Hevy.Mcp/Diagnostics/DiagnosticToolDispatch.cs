using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Hevy.Mcp.Diagnostics;

internal static class DiagnosticToolDispatch
{
  internal static async Task<CallToolResult> InvokeAsync(
      Func<CancellationToken, Task<CallToolResult>> action,
      DiagnosticOperationCategory category,
      RedactingLoggerProvider? diagnostics,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(action);

    var started = Stopwatch.GetTimestamp();
    var correlationId = Guid.NewGuid();
    try
    {
      var result = await action(cancellationToken);
      var operationEvent = SafeOperationEvent.FromToolResult(
          category,
          Stopwatch.GetElapsedTime(started),
          result,
          correlationId);
      diagnostics?.Write(LogLevelFor(operationEvent.Status), operationEvent);
      return result;
    }
    catch (OperationCanceledException)
    {
      diagnostics?.Write(
          LogLevel.Warning,
          SafeOperationEvent.Cancelled(category, Stopwatch.GetElapsedTime(started), correlationId));
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
