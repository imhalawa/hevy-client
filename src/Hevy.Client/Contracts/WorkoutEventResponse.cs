using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UpdatedWorkoutEventResponse), "updated")]
[JsonDerivedType(typeof(DeletedWorkoutEventResponse), "deleted")]
public abstract record WorkoutEventResponse;
