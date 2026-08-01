using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record WorkoutSetResponse([property: JsonRequired] int Index, [property: JsonRequired] string Type, decimal? WeightKg, decimal? Reps, decimal? DistanceMeters, decimal? DurationSeconds, decimal? Rpe, decimal? CustomMetric)
{
  internal WorkoutSet ToDomain() => new(Index, Type, WeightKg, Reps, DistanceMeters, DurationSeconds, Rpe, CustomMetric);
}
