namespace Hevy.Client.Models;

public sealed record UpdateWorkoutRequest(WorkoutWriteRequest Workout)
{
  public static implicit operator UpdateWorkoutRequest(UpdateWorkoutCommand value) => new(WorkoutWriteRequest.From(value.Workout));
}
