namespace Hevy.Mcp.Composite;

internal sealed record ExerciseVolumeObservation(
    string WorkoutId,
    DateTimeOffset StartTime,
    decimal VolumeKgReps);
