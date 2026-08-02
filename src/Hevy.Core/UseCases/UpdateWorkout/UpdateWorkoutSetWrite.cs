namespace Hevy.Core.UseCases;

public sealed record UpdateWorkoutSetWrite(
    SetType Type,
    decimal? WeightKg,
    int? Reps,
    int? DistanceMeters,
    int? DurationSeconds,
    decimal? CustomMetric,
    WorkoutRpe? Rpe);
