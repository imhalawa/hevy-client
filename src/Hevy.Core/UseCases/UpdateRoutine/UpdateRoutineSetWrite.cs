namespace Hevy.Core.UseCases;

public sealed record UpdateRoutineSetWrite(SetType Type, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? CustomMetric, RepRange? RepRange);
