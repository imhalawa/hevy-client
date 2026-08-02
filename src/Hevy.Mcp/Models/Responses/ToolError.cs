namespace Hevy.Mcp.Tools;

internal sealed record ToolError(
    string Code,
    string Message,
    bool Retryable,
    string CorrelationId,
    int? HevyStatus = null,
    string? HevyRequestId = null);
