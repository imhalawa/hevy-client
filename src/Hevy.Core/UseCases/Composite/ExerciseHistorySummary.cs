namespace Hevy.Core.UseCases;

public sealed record ExerciseHistorySummary(
    string MetricScope,
    string ExerciseTemplateId,
    int Weeks,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    int ChunkEntryCount,
    int ScannedEntryCount,
    decimal ChunkVolumeKgReps,
    decimal? ChunkProgressionKgReps,
    ExerciseVolumeObservation? FirstObservation,
    ExerciseVolumeObservation? LastObservation,
    ImmutableList<ExerciseHistoryEvidence> Evidence,
    bool Truncated,
    string? TruncationReason,
    string? Continuation,
    CompositeContinuationInputs? ContinuationInputs);
