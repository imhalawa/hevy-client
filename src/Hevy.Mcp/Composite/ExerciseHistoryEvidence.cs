namespace Hevy.Mcp.Composite;

internal sealed record ExerciseHistoryEvidence(
    string WorkoutId,
    DateTimeOffset WorkoutStartTime,
    decimal? VolumeKgReps);
