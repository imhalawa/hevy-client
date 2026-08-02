namespace Hevy.Core.UseCases;

public sealed record TrainingSummary(
    string MetricScope,
    int Weeks,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    int ChunkWorkoutFrequency,
    ImmutableList<WeeklyFrequency> WeeklyFrequency,
    ImmutableList<ExerciseTrainingSummary> Exercises,
    bool GapsComplete,
    ImmutableList<MissingWeekGap> MissingWeekGaps,
    ImmutableList<MeasurementDelta> MeasurementDeltas,
    ImmutableList<WorkoutEvidenceReference> Evidence,
    bool Truncated,
    string? Continuation,
    CompositeContinuationInputs? ContinuationInputs);
