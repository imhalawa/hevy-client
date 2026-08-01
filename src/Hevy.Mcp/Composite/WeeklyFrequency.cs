namespace Hevy.Mcp.Composite;

internal sealed record WeeklyFrequency(
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    int ChunkWorkoutCount,
    ImmutableList<WorkoutEvidenceReference> Evidence);
