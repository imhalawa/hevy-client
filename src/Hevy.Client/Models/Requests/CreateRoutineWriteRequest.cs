namespace Hevy.Client.Models;

public sealed record CreateRoutineWriteRequest(string Title, long? FolderId, string Notes, ImmutableList<CreateRoutineExerciseWriteRequest> Exercises)
{
  internal static CreateRoutineWriteRequest From(CreateRoutineWrite value) =>
      new(value.Title, value.FolderId, value.Notes, value.Exercises.Select(CreateRoutineExerciseWriteRequest.From).ToImmutableList());
}
