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
  internal bool IsValid() =>
      Enum.IsDefined(OperationCategory) &&
      Enum.IsDefined(DurationBucket) &&
      Enum.IsDefined(Status) &&
      Enum.IsDefined(ExceptionCategory) &&
      HttpStatus is null or >= 100 and <= 599;

  internal static SafeOperationEvent FromToolResult(
      DiagnosticOperationCategory category,
      TimeSpan elapsed,
      CallToolResult result,
      Guid fallbackCorrelationId,
      string operationName = "unknown")
  {
    var safeOperationName = SafeOperationName(operationName);
    if (result.IsError != true)
    {
      return new SafeOperationEvent(
          category,
          Bucket(elapsed),
          DiagnosticOperationStatus.Succeeded,
          fallbackCorrelationId,
          DiagnosticExceptionCategory.None,
          OperationName: safeOperationName);
    }

    if (ReadError(result) is not { } error)
    {
      return new SafeOperationEvent(
          category,
          Bucket(elapsed),
          DiagnosticOperationStatus.Failed,
          fallbackCorrelationId,
          DiagnosticExceptionCategory.Unexpected,
          OperationName: safeOperationName);
    }

    var (status, exceptionCategory) = ClassifyError(ReadString(error, "code"));
    return new SafeOperationEvent(
        category,
        Bucket(elapsed),
        status,
        ReadCorrelationId(error) ?? fallbackCorrelationId,
        exceptionCategory,
        ReadHttpStatus(error),
        safeOperationName,
        SafeIdentifier(ReadString(error, "hevy_request_id")));
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

  private static Guid? ReadCorrelationId(JsonElement error) =>
      error.TryGetProperty("correlation_id", out var value) &&
      value.ValueKind is JsonValueKind.String &&
      Guid.TryParseExact(value.GetString(), "N", out var correlationId)
          ? correlationId
          : null;

  private static JsonElement? ReadError(CallToolResult result) =>
      result.StructuredContent is { } content &&
      content.TryGetProperty("error", out var error) &&
      error.ValueKind is JsonValueKind.Object
          ? error
          : null;

  private static int? ReadHttpStatus(JsonElement error) =>
      error.TryGetProperty("hevy_status", out var value) &&
      value.ValueKind is JsonValueKind.Number &&
      value.TryGetInt32(out var status) &&
      status is >= 100 and <= 599
          ? status
          : null;

  private static string? ReadString(JsonElement error, string propertyName) =>
      error.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.String
          ? value.GetString()
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
