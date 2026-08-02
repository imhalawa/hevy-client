namespace Hevy.Client.Models;

public sealed record CreateRoutineExerciseWriteRequest(string ExerciseTemplateId, long? SupersetId, int? RestSeconds, string? Notes, ImmutableList<CreateRoutineSetWriteRequest> Sets)
{
  internal static CreateRoutineExerciseWriteRequest From(CreateRoutineExerciseWrite value) =>
      new(value.ExerciseTemplateId, value.SupersetId, value.RestSeconds, value.Notes, value.Sets.Select(CreateRoutineSetWriteRequest.From).ToImmutableList());
}
