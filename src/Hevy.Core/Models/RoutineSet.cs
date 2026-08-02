namespace Hevy.Core.Models;

public sealed record RoutineSet(int Index, string Type, decimal? WeightKg, decimal? Reps, decimal? DistanceMeters, decimal? DurationSeconds, decimal? Rpe, decimal? CustomMetric, RepRange? RepRange) : SetMetrics(WeightKg, Reps, DistanceMeters, DurationSeconds, Rpe, CustomMetric);
