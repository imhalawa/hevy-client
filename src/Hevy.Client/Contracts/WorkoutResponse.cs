using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record WorkoutResponse([property: JsonRequired] string Id, [property: JsonRequired] string Title, [property: JsonRequired] string RoutineId, [property: JsonRequired] string Description, [property: JsonRequired] DateTimeOffset StartTime, [property: JsonRequired] DateTimeOffset EndTime, [property: JsonRequired] DateTimeOffset UpdatedAt, [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] ImmutableList<WorkoutExerciseResponse> Exercises);
