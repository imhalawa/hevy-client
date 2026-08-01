using System.Globalization;
using Hevy.Client;
using Hevy.Core.Models;

namespace Hevy.Mcp.Composite;

internal sealed record TrainingSummary(
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
