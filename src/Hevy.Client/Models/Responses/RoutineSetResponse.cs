using System.Text.Json.Serialization;
using Hevy.Core.Models;

namespace Hevy.Client.Models;

public sealed record RoutineSetResponse([property: JsonRequired] int Index, [property: JsonRequired] string Type, decimal? WeightKg, decimal? Reps, decimal? DistanceMeters, decimal? DurationSeconds, decimal? Rpe, decimal? CustomMetric, RepRange? RepRange)
{
  internal RoutineSet ToDomain() => new(Index, Type, WeightKg, Reps, DistanceMeters, DurationSeconds, Rpe, CustomMetric, RepRange);
}
