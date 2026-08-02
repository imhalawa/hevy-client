using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record WorkoutSetWriteRequest([property: JsonConverter(typeof(SetTypeApiJsonConverter))] SetTypeApi Type, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? CustomMetric, [property: JsonConverter(typeof(WorkoutRpeJsonConverter))] WorkoutRpe? Rpe)
{
  internal static WorkoutSetWriteRequest From(CreateWorkoutSetWrite value) =>
      new((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.Rpe);

  internal static WorkoutSetWriteRequest From(UpdateWorkoutSetWrite value) =>
      new((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.Rpe);
}
