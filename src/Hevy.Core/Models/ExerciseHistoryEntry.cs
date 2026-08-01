using System;

namespace Hevy.Core.Models;

public sealed record ExerciseHistoryEntry(string WorkoutId, string WorkoutTitle, DateTimeOffset WorkoutStartTime, DateTimeOffset WorkoutEndTime, string ExerciseTemplateId, decimal? WeightKg, int? Reps, int? DistanceMeters, int? DurationSeconds, decimal? Rpe, decimal? CustomMetric, string SetType);
