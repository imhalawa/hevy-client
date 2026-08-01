using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Hevy.Mcp.Diagnostics;

internal sealed record SafeOperationEvent(
    DiagnosticOperationCategory OperationCategory,
    DiagnosticDurationBucket DurationBucket,
    DiagnosticOperationStatus Status,
    Guid CorrelationId,
    DiagnosticExceptionCategory ExceptionCategory,
    int? HttpStatus = null,
    string OperationName = "unknown",
    string? HevyRequestId = null)
{
  internal static SafeOperationEvent FromToolResult(
      DiagnosticOperationCategory category,
      TimeSpan elapsed,
      CallToolResult result,
      Guid fallbackCorrelationId,
      string operationName = "unknown")
  {
    ArgumentNullException.ThrowIfNull(result);

    var correlationId = fallbackCorrelationId;
    var exceptionCategory = DiagnosticExceptionCategory.None;
    var status = DiagnosticOperationStatus.Succeeded;
    int? httpStatus = null;
    string? hevyRequestId = null;
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

        if (error.TryGetProperty("hevy_request_id", out var requestIdValue) && requestIdValue.ValueKind is JsonValueKind.String)
        {
          hevyRequestId = SafeIdentifier(requestIdValue.GetString());
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

    return new SafeOperationEvent(category, Bucket(elapsed), status, correlationId, exceptionCategory, httpStatus, SafeOperationName(operationName), hevyRequestId);
  }

  internal static SafeOperationEvent Cancelled(
      DiagnosticOperationCategory category,
      TimeSpan elapsed,
      Guid correlationId,
      string operationName = "unknown") => new(
          category,
          Bucket(elapsed),
          DiagnosticOperationStatus.Cancelled,
          correlationId,
          DiagnosticExceptionCategory.Cancellation,
          OperationName: SafeOperationName(operationName));

  private static string SafeOperationName(string value) =>
      value.Length is >= 1 and <= 64 && char.IsAsciiLetterLower(value[0]) && value.All(static character => char.IsAsciiLetterOrDigit(character) || character == '_')
          ? value
          : "unknown";

  private static string? SafeIdentifier(string? value) =>
      value is { Length: >= 1 and <= 128 } && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-')
          ? value
          : null;

  private static (DiagnosticOperationStatus Status, DiagnosticExceptionCategory Exception) ClassifyError(string? code) => code switch
  {
    "validation" or "validation_error" or "conflict" or "not_found" or "authentication" or "authorization" =>
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
