namespace Hevy.Client.Models;

public sealed record CreateRoutineWriteRequest(string Title, long? FolderId, string Notes, ImmutableList<CreateRoutineExerciseWriteRequest> Exercises)
{
  internal static CreateRoutineWriteRequest From(CreateRoutineWrite value) =>
      new(value.Title, value.FolderId, value.Notes, value.Exercises.Select(CreateRoutineExerciseWriteRequest.From).ToImmutableList());

  internal CreateRoutineWrite ToDomain() =>
      new(Title, FolderId, Notes, Exercises.Select(static exercise => exercise.ToDomain()).ToImmutableList());
}
