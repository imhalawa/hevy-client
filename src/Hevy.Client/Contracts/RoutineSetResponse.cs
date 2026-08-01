using System.Text.Json.Serialization;
using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record RoutineSetResponse([property: JsonRequired] int Index, [property: JsonRequired] string Type, decimal? WeightKg, decimal? Reps, decimal? DistanceMeters, decimal? DurationSeconds, decimal? Rpe, decimal? CustomMetric, RepRange? RepRange);
