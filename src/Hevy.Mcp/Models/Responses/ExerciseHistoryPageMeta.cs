namespace Hevy.Mcp.Tools;

internal sealed record ExerciseHistoryPageMeta(
    int Page,
    int PageSize,
    string Detail,
    int ScannedItemCount,
    bool Truncated,
    string? TruncationReason,
    ExerciseHistoryContinuation? Continuation = null);
