namespace Hevy.Core.UseCases;

public sealed record CreateRoutineWrite(string Title, long? FolderId, string Notes, ImmutableList<CreateRoutineExerciseWrite> Exercises)
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Title)) throw new ArgumentException("A routine title is required.", nameof(Title));
    foreach (var exercise in Exercises) exercise.Validate();
  }
}
