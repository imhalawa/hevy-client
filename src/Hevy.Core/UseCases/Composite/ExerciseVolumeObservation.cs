namespace Hevy.Core.UseCases;

public sealed record ExerciseVolumeObservation(
    string WorkoutId,
    DateTimeOffset StartTime,
    decimal VolumeKgReps);
