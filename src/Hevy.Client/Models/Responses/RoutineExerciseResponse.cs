using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record RoutineExerciseResponse([property: JsonRequired] int Index, [property: JsonRequired] string Title, [property: JsonRequired] string RestSeconds, [property: JsonRequired] string Notes, [property: JsonRequired] string ExerciseTemplateId, [property: JsonPropertyName("supersets_id")] long? SupersetId, [property: JsonRequired] ImmutableList<RoutineSetResponse> Sets)
{
  internal RoutineExercise ToDomain() => new(Index, Title, RestSeconds, Notes, ExerciseTemplateId, SupersetId, Sets.Select(static set => set.ToDomain()).ToImmutableList());
}
