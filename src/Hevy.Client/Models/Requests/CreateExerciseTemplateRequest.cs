namespace Hevy.Client.Models;

public sealed record CreateExerciseTemplateRequest(CustomExerciseWriteRequest Exercise)
{
  public static implicit operator CreateExerciseTemplateRequest(CreateExerciseTemplateCommand value) => new(CustomExerciseWriteRequest.From(value.Exercise));
}
