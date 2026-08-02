namespace Hevy.Core.UseCases;

public sealed record UpdateRoutineExerciseWrite(string ExerciseTemplateId, long? SupersetId, int? RestSeconds, string? Notes, ImmutableList<UpdateRoutineSetWrite> Sets)
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(ExerciseTemplateId)) throw new ArgumentException("An exercise template id is required.", nameof(ExerciseTemplateId));
  }
}
