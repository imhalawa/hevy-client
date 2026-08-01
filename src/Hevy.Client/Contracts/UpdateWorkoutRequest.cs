using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record UpdateWorkoutRequest(WorkoutWriteRequest Workout)
{
  public static implicit operator UpdateWorkoutRequest(UpdateWorkoutCommand value)
  {
    return new UpdateWorkoutRequest(value.Workout.ToRequest());
  }

  public static implicit operator UpdateWorkoutCommand(UpdateWorkoutRequest value)
  {
    return value.ToCommand();
  }
}
