using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record RoutineResponse([property: JsonRequired] string Id, [property: JsonRequired] string Title, long? FolderId, [property: JsonRequired] DateTimeOffset UpdatedAt, [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] ImmutableList<RoutineExerciseResponse> Exercises)
{
  internal Routine ToDomain() => new(Id, Title, FolderId, UpdatedAt, CreatedAt, Exercises.Select(static exercise => exercise.ToDomain()).ToImmutableList());
}
