using System.Text.Json.Serialization;
using Hevy.Core.Models;

namespace Hevy.Client.Models;

public sealed record UpdateRoutineSetWriteRequest([property: JsonConverter(typeof(SetTypeApiJsonConverter))] SetTypeApi Type, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? CustomMetric, RepRange? RepRange)
{
  internal static UpdateRoutineSetWriteRequest From(UpdateRoutineSetWrite value) =>
      new((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.RepRange);

  internal UpdateRoutineSetWrite ToDomain() => new((SetType)Type, WeightKg, Reps, DistanceMeters, DurationSeconds, CustomMetric, RepRange);
}
