using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record UpdatedWorkoutEventResponse([property: JsonRequired] WorkoutResponse Workout) : WorkoutEventResponse
{
  public override void Validate() => Workout.Validate();

  internal override WorkoutEvent ToDomain() => new UpdatedWorkoutEvent(Workout.ToDomain());
}
