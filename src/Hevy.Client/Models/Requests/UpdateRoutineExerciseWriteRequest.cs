namespace Hevy.Client.Models;

public sealed record UpdateRoutineExerciseWriteRequest(string ExerciseTemplateId, long? SupersetId, int? RestSeconds, string? Notes, ImmutableList<UpdateRoutineSetWriteRequest> Sets)
{
  internal static UpdateRoutineExerciseWriteRequest From(UpdateRoutineExerciseWrite value) =>
      new(value.ExerciseTemplateId, value.SupersetId, value.RestSeconds, value.Notes, value.Sets.Select(UpdateRoutineSetWriteRequest.From).ToImmutableList());
}
