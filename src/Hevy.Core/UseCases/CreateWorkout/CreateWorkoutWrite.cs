namespace Hevy.Core.UseCases;

public sealed record CreateWorkoutWrite(string Title, string? Description, DateTimeOffset StartTime, DateTimeOffset EndTime, bool IsPrivate, ImmutableList<CreateWorkoutExerciseWrite> Exercises)
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Title)) throw new ArgumentException("A workout title is required.", nameof(Title));
    if (EndTime < StartTime) throw new ArgumentException("Workout end time cannot be before its start time.", nameof(EndTime));
    foreach (var exercise in Exercises) exercise.Validate();
  }
}
