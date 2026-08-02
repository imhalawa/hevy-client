namespace Hevy.Mcp.Diagnostics;

internal sealed record DiagnosticLogRecord(
    string ServerVersion,
    string RuntimeVersion,
    string Transport,
    bool ReadOnly,
    DiagnosticOperationCategory OperationCategory,
    string OperationName,
    DiagnosticDurationBucket DurationBucket,
    DiagnosticOperationStatus Status,
    string CorrelationId,
    DiagnosticExceptionCategory ExceptionCategory,
    int? HttpStatus,
    string? HevyRequestId);
