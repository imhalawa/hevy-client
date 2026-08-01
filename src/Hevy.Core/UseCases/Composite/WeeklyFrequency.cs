namespace Hevy.Core.UseCases;

public sealed record WeeklyFrequency(
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    int ChunkWorkoutCount,
    ImmutableList<WorkoutEvidenceReference> Evidence);
