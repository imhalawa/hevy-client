using System.Text.Json.Serialization;
using Hevy.Core.Models;

namespace Hevy.Client.Models;

public sealed record CreateRoutineSetWriteRequest([property: JsonConverter(typeof(SetTypeApiJsonConverter))] SetTypeApi Type, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? CustomMetric, CreateRoutineRepRange? RepRange)
{
  internal static CreateRoutineSetWriteRequest From(CreateRoutineSetWrite value) =>
      new((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.RepRange);

  internal CreateRoutineSetWrite ToDomain() => new((SetType)Type, WeightKg, Reps, DistanceMeters, DurationSeconds, CustomMetric, RepRange);
}
