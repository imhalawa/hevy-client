namespace Hevy.Core.UseCases;

public sealed record UpdateWorkoutExerciseWrite(
    string ExerciseTemplateId,
    long? SupersetId,
    string? Notes,
    ImmutableList<UpdateWorkoutSetWrite> Sets)
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(ExerciseTemplateId)) throw new ArgumentException("An exercise template id is required.", nameof(ExerciseTemplateId));
  }
}
