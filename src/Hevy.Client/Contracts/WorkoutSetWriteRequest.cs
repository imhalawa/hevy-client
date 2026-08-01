using System.Text.Json.Serialization;
using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record WorkoutSetWriteRequest([property: JsonConverter(typeof(SetTypeApiJsonConverter))] SetTypeApi Type, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? CustomMetric, [property: JsonConverter(typeof(WorkoutRpeJsonConverter))] WorkoutRpe? Rpe);
