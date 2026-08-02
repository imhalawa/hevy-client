using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UpdatedWorkoutEventResponse), "updated")]
[JsonDerivedType(typeof(DeletedWorkoutEventResponse), "deleted")]
public abstract record WorkoutEventResponse
{
  public abstract void Validate();

  internal abstract WorkoutEvent ToDomain();
}
