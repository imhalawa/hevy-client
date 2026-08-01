using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record CreateWorkoutRequest(WorkoutWriteRequest Workout)
{
  public static implicit operator CreateWorkoutRequest(CreateWorkoutCommand value)
  {
    return new CreateWorkoutRequest(value.Workout.ToRequest());
  }

  public static implicit operator CreateWorkoutCommand(CreateWorkoutRequest value)
  {
    return value.ToCommand();
  }
}
