namespace Hevy.Mcp.Composite;

internal sealed record CompositeContinuationInputs(
    int Weeks,
    DateTimeOffset RangeEndUtc,
    int Limit,
    string Continuation);
