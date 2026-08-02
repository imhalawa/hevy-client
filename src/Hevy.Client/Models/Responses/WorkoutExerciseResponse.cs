using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record WorkoutExerciseResponse([property: JsonRequired] int Index, [property: JsonRequired] string Title, [property: JsonRequired] string Notes, [property: JsonRequired] string ExerciseTemplateId, [property: JsonPropertyName("supersets_id")] long? SupersetId, [property: JsonRequired] ImmutableList<WorkoutSetResponse> Sets)
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(ExerciseTemplateId)) throw new JsonException();
    if (Sets.Any(static set => set is null)) throw new JsonException();
  }

  internal WorkoutExercise ToDomain() => new(Index, Title, Notes, ExerciseTemplateId, SupersetId, Sets.Select(static set => set.ToDomain()).ToImmutableList());
}
