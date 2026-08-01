using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record WorkoutSetResponse([property: JsonRequired] int Index, [property: JsonRequired] string Type, decimal? WeightKg, decimal? Reps, decimal? DistanceMeters, decimal? DurationSeconds, decimal? Rpe, decimal? CustomMetric);
