namespace Hevy.Client.Models;

public sealed record CreateWorkoutRequest(WorkoutWriteRequest Workout)
{
  public static implicit operator CreateWorkoutRequest(CreateWorkoutCommand value) => new(WorkoutWriteRequest.From(value.Workout));
}
