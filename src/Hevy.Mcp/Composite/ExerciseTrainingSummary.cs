namespace Hevy.Mcp.Composite;

internal sealed record ExerciseTrainingSummary(
    string ExerciseTemplateId,
    string Title,
    decimal ChunkVolumeKgReps,
    decimal? ChunkProgressionKgReps,
    ExerciseVolumeObservation FirstObservation,
    ExerciseVolumeObservation LastObservation,
    ImmutableList<WorkoutEvidenceReference> Evidence);
