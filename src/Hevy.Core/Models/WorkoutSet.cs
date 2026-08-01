namespace Hevy.Core.Models;

public sealed record WorkoutSet(int Index, string Type, decimal? WeightKg, decimal? Reps, decimal? DistanceMeters, decimal? DurationSeconds, decimal? Rpe, decimal? CustomMetric) : SetMetrics(WeightKg, Reps, DistanceMeters, DurationSeconds, Rpe, CustomMetric);
