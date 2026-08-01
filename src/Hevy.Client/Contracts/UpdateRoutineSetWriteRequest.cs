using System.Text.Json.Serialization;
using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record UpdateRoutineSetWriteRequest([property: JsonConverter(typeof(SetTypeApiJsonConverter))] SetTypeApi Type, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? CustomMetric, RepRange? RepRange);
