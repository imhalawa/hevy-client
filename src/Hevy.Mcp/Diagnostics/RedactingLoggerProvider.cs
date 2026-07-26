using System.Text.Json;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Hevy.Mcp.Diagnostics;

internal enum DiagnosticOperationCategory
{
  Read,
  Mutation,
  Composite,
  Diagnostics,
  Protocol,
}

internal enum DiagnosticDurationBucket
{
  UnderOneHundredMilliseconds,
  UnderOneSecond,
  UnderTenSeconds,
  TenSecondsOrMore,
}

internal enum DiagnosticOperationStatus
{
  Succeeded,
  Rejected,
  Failed,
  Cancelled,
}

internal enum DiagnosticExceptionCategory
{
  None,
  Validation,
  Upstream,
  OutcomeUnknown,
  Unexpected,
  Cancellation,
}

internal sealed record SafeOperationEvent(
    DiagnosticOperationCategory OperationCategory,
    DiagnosticDurationBucket DurationBucket,
    DiagnosticOperationStatus Status,
    Guid CorrelationId,
    DiagnosticExceptionCategory ExceptionCategory,
    int? HttpStatus = null)
{
  internal static SafeOperationEvent FromToolResult(
      DiagnosticOperationCategory category,
      TimeSpan elapsed,
      CallToolResult result,
      Guid fallbackCorrelationId)
  {
    ArgumentNullException.ThrowIfNull(result);

    var correlationId = fallbackCorrelationId;
    var exceptionCategory = DiagnosticExceptionCategory.None;
    var status = DiagnosticOperationStatus.Succeeded;
    int? httpStatus = null;
    if (result.IsError == true && result.StructuredContent is { } content)
    {
      status = DiagnosticOperationStatus.Failed;
      if (content.TryGetProperty("error", out var error) && error.ValueKind is JsonValueKind.Object)
      {
        if (error.TryGetProperty("correlation_id", out var correlationValue) &&
            correlationValue.ValueKind is JsonValueKind.String &&
            Guid.TryParseExact(correlationValue.GetString(), "N", out var parsedCorrelationId))
        {
          correlationId = parsedCorrelationId;
        }

        if (error.TryGetProperty("hevy_status", out var statusValue) &&
            statusValue.ValueKind is JsonValueKind.Number &&
            statusValue.TryGetInt32(out var parsedStatus) &&
            parsedStatus is >= 100 and <= 599)
        {
          httpStatus = parsedStatus;
        }

        var code = error.TryGetProperty("code", out var codeValue) && codeValue.ValueKind is JsonValueKind.String
            ? codeValue.GetString()
            : null;
        (status, exceptionCategory) = ClassifyError(code);
      }
      else
      {
        exceptionCategory = DiagnosticExceptionCategory.Unexpected;
      }
    }

    return new SafeOperationEvent(category, Bucket(elapsed), status, correlationId, exceptionCategory, httpStatus);
  }

  internal static SafeOperationEvent Cancelled(
      DiagnosticOperationCategory category,
      TimeSpan elapsed,
      Guid correlationId) => new(
          category,
          Bucket(elapsed),
          DiagnosticOperationStatus.Cancelled,
          correlationId,
          DiagnosticExceptionCategory.Cancellation);

  private static (DiagnosticOperationStatus Status, DiagnosticExceptionCategory Exception) ClassifyError(string? code) => code switch
  {
    "validation_error" or "conflict" or "not_found" or "authentication" or "authorization" =>
        (DiagnosticOperationStatus.Rejected, DiagnosticExceptionCategory.Validation),
    "outcome_unknown" => (DiagnosticOperationStatus.Failed, DiagnosticExceptionCategory.OutcomeUnknown),
    "rate_limited" or "transient_upstream" or "timeout" or "unexpected_response" =>
        (DiagnosticOperationStatus.Failed, DiagnosticExceptionCategory.Upstream),
    _ => (DiagnosticOperationStatus.Failed, DiagnosticExceptionCategory.Unexpected),
  };

  private static DiagnosticDurationBucket Bucket(TimeSpan elapsed) => elapsed switch
  {
    { TotalMilliseconds: < 100 } => DiagnosticDurationBucket.UnderOneHundredMilliseconds,
    { TotalSeconds: < 1 } => DiagnosticDurationBucket.UnderOneSecond,
    { TotalSeconds: < 10 } => DiagnosticDurationBucket.UnderTenSeconds,
    _ => DiagnosticDurationBucket.TenSecondsOrMore,
  };
}

internal sealed record DiagnosticLogRecord(
    string ServerVersion,
    string RuntimeVersion,
    string Transport,
    bool ReadOnly,
    DiagnosticOperationCategory OperationCategory,
    DiagnosticDurationBucket DurationBucket,
    DiagnosticOperationStatus Status,
    string CorrelationId,
    DiagnosticExceptionCategory ExceptionCategory,
    int? HttpStatus);

internal sealed class RedactingLoggerProvider : ILoggerProvider
{
  private readonly Lock writeLock = new();
  private readonly TextWriter writer;
  private readonly DiagnosticSnapshot snapshot;
  private readonly LogLevel minimumLevel;

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
    ArgumentNullException.ThrowIfNull(operationEvent);
    if (!IsEnabled(logLevel) ||
        !Enum.IsDefined(operationEvent.OperationCategory) ||
        !Enum.IsDefined(operationEvent.DurationBucket) ||
        !Enum.IsDefined(operationEvent.Status) ||
        !Enum.IsDefined(operationEvent.ExceptionCategory) ||
        operationEvent.HttpStatus is not (null or >= 100 and <= 599))
    {
      return;
    }

    var record = new DiagnosticLogRecord(
        snapshot.ServerVersion,
        snapshot.RuntimeVersion,
        snapshot.Transport,
        snapshot.ReadOnly,
        operationEvent.OperationCategory,
        operationEvent.DurationBucket,
        operationEvent.Status,
        operationEvent.CorrelationId.ToString("N"),
        operationEvent.ExceptionCategory,
        operationEvent.HttpStatus);
    var line = JsonSerializer.Serialize(record, ToolResults.JsonOptions);
    lock (writeLock)
    {
      writer.WriteLine(line);
      writer.Flush();
    }
  }

  private bool IsEnabled(LogLevel logLevel) =>
      logLevel is not LogLevel.None && logLevel >= minimumLevel;

  private sealed class AllowlistLogger(RedactingLoggerProvider provider) : ILogger
  {
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
      if (state is SafeOperationEvent operationEvent)
      {
        provider.Write(logLevel, operationEvent);
      }
    }
  }
}
