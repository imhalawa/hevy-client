using System;
using System.Collections.Immutable;

namespace Hevy.Core.Models;

public sealed record Workout(string Id, string Title, string RoutineId, string Description, DateTimeOffset StartTime, DateTimeOffset EndTime, DateTimeOffset UpdatedAt, DateTimeOffset CreatedAt, ImmutableList<WorkoutExercise> Exercises);
