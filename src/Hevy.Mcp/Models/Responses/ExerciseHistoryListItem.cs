namespace Hevy.Mcp.Tools;

internal sealed record ExerciseHistoryListItem(
    string WorkoutId,
    string WorkoutTitle,
    DateTimeOffset WorkoutStartTime,
    string ExerciseTemplateId,
    string SetType,
    DateTimeOffset? WorkoutEndTime = null,
    decimal? WeightKg = null,
    int? Reps = null,
    int? DistanceMeters = null,
    int? DurationSeconds = null,
    decimal? Rpe = null,
    decimal? CustomMetric = null);
