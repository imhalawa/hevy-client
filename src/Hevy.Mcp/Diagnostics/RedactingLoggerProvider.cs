using System.Text.Json;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Tools;

namespace Hevy.Mcp.Diagnostics;

internal sealed class RedactingLoggerProvider : ILoggerProvider
{
  private readonly Lock writeLock = new();
  private readonly TextWriter writer;
  private readonly DiagnosticSnapshot snapshot;
  private readonly LogLevel minimumLevel;
  private bool sinkDisabled;

  private RedactingLoggerProvider(TextWriter writer, DiagnosticSnapshot snapshot, LogLevel minimumLevel)
  {
    this.writer = writer;
    this.snapshot = snapshot;
    this.minimumLevel = minimumLevel;
  }

  internal static RedactingLoggerProvider? Create(HevyMcpOptions options, TextWriter writer)
  {
    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(writer);
    return options.LogLevel is LogLevel.None
        ? null
        : new RedactingLoggerProvider(writer, DiagnosticSnapshot.Create(options), options.LogLevel);
  }

  public ILogger CreateLogger(string categoryName) => new AllowlistLogger(this);

  public void Dispose()
  {
  }

  internal void Write(LogLevel logLevel, SafeOperationEvent operationEvent)
  {
    if (!IsEnabled(logLevel) ||
        !Enum.IsDefined(operationEvent.OperationCategory) ||
        !Enum.IsDefined(operationEvent.DurationBucket) ||
        !Enum.IsDefined(operationEvent.Status) ||
        !Enum.IsDefined(operationEvent.ExceptionCategory) ||
        operationEvent.HttpStatus is not (null or >= 100 and <= 599))
    {
      return;
    }

    lock (writeLock)
    {
      if (sinkDisabled)
      {
        return;
      }

      try
      {
        var record = new DiagnosticLogRecord(
            snapshot.ServerVersion,
            snapshot.RuntimeVersion,
            snapshot.Transport,
            snapshot.ReadOnly,
            operationEvent.OperationCategory,
            operationEvent.OperationName,
            operationEvent.DurationBucket,
            operationEvent.Status,
            operationEvent.CorrelationId.ToString("N"),
            operationEvent.ExceptionCategory,
            operationEvent.HttpStatus,
            operationEvent.HevyRequestId);
        var line = JsonSerializer.Serialize(record, ToolResults.JsonOptions);
        writer.WriteLine(line);
        writer.Flush();
      }
      catch (Exception)
      {
        Volatile.Write(ref sinkDisabled, true);
      }
    }
  }

  internal bool IsEnabled(LogLevel logLevel) =>
      !Volatile.Read(ref sinkDisabled) && logLevel is not LogLevel.None && logLevel >= minimumLevel;
}
