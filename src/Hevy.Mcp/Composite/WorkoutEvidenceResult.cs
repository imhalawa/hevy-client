namespace Hevy.Mcp.Composite;

internal sealed record WorkoutEvidenceResult(
    ImmutableList<WorkoutEvidenceItem> Items,
    int Weeks,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    bool Truncated,
    string? Continuation,
    CompositeContinuationInputs? ContinuationInputs);
