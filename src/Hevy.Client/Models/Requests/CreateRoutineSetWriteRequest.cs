using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record CreateRoutineSetWriteRequest([property: JsonConverter(typeof(SetTypeApiJsonConverter))] SetTypeApi Type, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? CustomMetric, CreateRoutineRepRange? RepRange)
{
  internal static CreateRoutineSetWriteRequest From(CreateRoutineSetWrite value) =>
      new((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.RepRange);
}
