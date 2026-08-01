using Hevy.Core.Models;

namespace Hevy.Client.Models;

public sealed record CreateWorkoutRequest(WorkoutWriteRequest Workout)
{
  public static implicit operator CreateWorkoutRequest(CreateWorkoutCommand value) => new(WorkoutWriteRequest.From(value.Workout));

  public static implicit operator CreateWorkoutCommand(CreateWorkoutRequest value) => new(value.Workout.ToCreateWorkout());
}
