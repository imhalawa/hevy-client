using Hevy.Core.Models;

namespace Hevy.Client.Models;

public sealed record UpdateWorkoutRequest(WorkoutWriteRequest Workout)
{
  public static implicit operator UpdateWorkoutRequest(UpdateWorkoutCommand value) => new(WorkoutWriteRequest.From(value.Workout));

  public static implicit operator UpdateWorkoutCommand(UpdateWorkoutRequest value) => new(value.Workout.ToUpdateWorkout());
}
