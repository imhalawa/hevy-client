namespace Hevy.Client.Models;

public sealed record UpdateRoutineWriteRequest(string Title, string? Notes, ImmutableList<UpdateRoutineExerciseWriteRequest> Exercises)
{
  internal static UpdateRoutineWriteRequest From(UpdateRoutineWrite value) =>
      new(value.Title, value.Notes, value.Exercises.Select(UpdateRoutineExerciseWriteRequest.From).ToImmutableList());

  internal UpdateRoutineWrite ToDomain() =>
      new(Title, Notes, Exercises.Select(static exercise => exercise.ToDomain()).ToImmutableList());
}
