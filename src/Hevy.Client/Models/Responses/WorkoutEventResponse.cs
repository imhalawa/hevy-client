using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UpdatedWorkoutEventResponse), "updated")]
[JsonDerivedType(typeof(DeletedWorkoutEventResponse), "deleted")]
public abstract record WorkoutEventResponse
{
  internal WorkoutEvent ToDomain()
  {
    if (this is UpdatedWorkoutEventResponse updated)
    {
      return new UpdatedWorkoutEvent(updated.Workout.ToDomain());
    }

    if (this is DeletedWorkoutEventResponse deleted)
    {
      return new DeletedWorkoutEvent(deleted.Id, deleted.DeletedAt);
    }

    throw new InvalidOperationException("Unsupported workout event response.");
  }
}
