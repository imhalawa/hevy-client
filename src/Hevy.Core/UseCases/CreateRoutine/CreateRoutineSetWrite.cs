namespace Hevy.Core.UseCases;

public sealed record CreateRoutineSetWrite(SetType Type, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? CustomMetric, CreateRoutineRepRange? RepRange);
