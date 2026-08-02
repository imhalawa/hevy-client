namespace Hevy.Client.Models;

public sealed record WorkoutWriteRequest(string Title, string? Description, DateTimeOffset StartTime, DateTimeOffset EndTime, bool IsPrivate, ImmutableList<WorkoutExerciseWriteRequest> Exercises)
{
  internal static WorkoutWriteRequest From(CreateWorkoutWrite value) =>
      new(value.Title, value.Description, value.StartTime, value.EndTime, value.IsPrivate, value.Exercises.Select(WorkoutExerciseWriteRequest.From).ToImmutableList());

  internal static WorkoutWriteRequest From(UpdateWorkoutWrite value) =>
      new(value.Title, value.Description, value.StartTime, value.EndTime, value.IsPrivate, value.Exercises.Select(WorkoutExerciseWriteRequest.From).ToImmutableList());
}
