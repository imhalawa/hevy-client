namespace Hevy.Core.UseCases;

public sealed record ExerciseHistoryEvidence(
    string WorkoutId,
    DateTimeOffset WorkoutStartTime,
    decimal? VolumeKgReps);
