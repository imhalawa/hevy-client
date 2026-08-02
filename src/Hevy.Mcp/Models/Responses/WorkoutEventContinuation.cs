namespace Hevy.Mcp.Tools;

internal sealed record WorkoutEventContinuation(
    int Page,
    int PageSize,
    DateTimeOffset Since,
    string Detail);
