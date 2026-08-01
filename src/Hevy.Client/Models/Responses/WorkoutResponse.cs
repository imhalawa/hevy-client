using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record WorkoutResponse([property: JsonRequired] string Id, [property: JsonRequired] string Title, [property: JsonRequired] string RoutineId, [property: JsonRequired] string Description, [property: JsonRequired] DateTimeOffset StartTime, [property: JsonRequired] DateTimeOffset EndTime, [property: JsonRequired] DateTimeOffset UpdatedAt, [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] ImmutableList<WorkoutExerciseResponse> Exercises)
{
  internal Workout ToDomain() => new(Id, Title, RoutineId, Description, StartTime, EndTime, UpdatedAt, CreatedAt, Exercises.Select(static exercise => exercise.ToDomain()).ToImmutableList());
}
