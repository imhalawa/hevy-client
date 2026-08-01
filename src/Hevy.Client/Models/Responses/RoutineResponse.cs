using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record RoutineResponse([property: JsonRequired] string Id, [property: JsonRequired] string Title, long? FolderId, [property: JsonRequired] DateTimeOffset UpdatedAt, [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] ImmutableList<RoutineExerciseResponse> Exercises) : IHevyResponse
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Id) || UpdatedAt == default || CreatedAt == default) throw new JsonException();
    foreach (var exercise in Exercises)
    {
      if (exercise is null) throw new JsonException();
      exercise.Validate();
    }
  }

  internal Routine ToDomain() => new(Id, Title, FolderId, UpdatedAt, CreatedAt, Exercises.Select(static exercise => exercise.ToDomain()).ToImmutableList());
}
