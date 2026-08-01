namespace Hevy.Core.UseCases;

public sealed record UpdateRoutineWrite(string Title, string? Notes, ImmutableList<UpdateRoutineExerciseWrite> Exercises)
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Title)) throw new ArgumentException("A routine title is required.", nameof(Title));
    foreach (var exercise in Exercises) exercise.Validate();
  }
}
