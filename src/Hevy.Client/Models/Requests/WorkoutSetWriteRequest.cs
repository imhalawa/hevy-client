using System.Text.Json;
using System.Text.Json.Serialization;
using Hevy.Core.Models;

namespace Hevy.Client.Models;

public sealed record WorkoutSetWriteRequest([property: JsonConverter(typeof(SetTypeApiJsonConverter))] SetTypeApi Type, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? CustomMetric, [property: JsonConverter(typeof(WorkoutRpeJsonConverter))] WorkoutRpe? Rpe)
{
  internal static WorkoutSetWriteRequest From(CreateWorkoutSetWrite value)
  {
    ValidateRpe(value.Rpe);
    return new((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.Rpe);
  }

  internal static WorkoutSetWriteRequest From(UpdateWorkoutSetWrite value)
  {
    ValidateRpe(value.Rpe);
    return new((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.Rpe);
  }

  internal CreateWorkoutSetWrite ToCreateWorkout() => new((SetType)Type, WeightKg, Reps, DistanceMeters, DurationSeconds, CustomMetric, Rpe);

  internal UpdateWorkoutSetWrite ToUpdateWorkout() => new((SetType)Type, WeightKg, Reps, DistanceMeters, DurationSeconds, CustomMetric, Rpe);

  private static void ValidateRpe(WorkoutRpe? rpe)
  {
    if (rpe.HasValue && !WorkoutRpe.IsValid(rpe.GetValueOrDefault().Value))
    {
      throw new JsonException("RPE is invalid.");
    }
  }
}
