using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record WorkoutExerciseResponse([property: JsonRequired] int Index, [property: JsonRequired] string Title, [property: JsonRequired] string Notes, [property: JsonRequired] string ExerciseTemplateId, [property: JsonPropertyName("supersets_id")] long? SupersetId, [property: JsonRequired] ImmutableList<WorkoutSetResponse> Sets)
{
  internal WorkoutExercise ToDomain() => new(Index, Title, Notes, ExerciseTemplateId, SupersetId, Sets.Select(static set => set.ToDomain()).ToImmutableList());
}
