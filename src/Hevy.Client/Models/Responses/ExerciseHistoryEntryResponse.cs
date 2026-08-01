using System;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record ExerciseHistoryEntryResponse([property: JsonRequired] string WorkoutId, [property: JsonRequired] string WorkoutTitle, [property: JsonRequired] DateTimeOffset WorkoutStartTime, [property: JsonRequired] DateTimeOffset WorkoutEndTime, [property: JsonRequired] string ExerciseTemplateId, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? Rpe, decimal? CustomMetric, [property: JsonRequired] string SetType)
{
  internal ExerciseHistoryEntry ToDomain() => new(WorkoutId, WorkoutTitle, WorkoutStartTime, WorkoutEndTime, ExerciseTemplateId, WeightKg, Reps, DistanceMeters, DurationSeconds, Rpe, CustomMetric, SetType);
}
