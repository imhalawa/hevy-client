using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record WorkoutResponse([property: JsonRequired] string Id, [property: JsonRequired] string Title, [property: JsonRequired] string RoutineId, [property: JsonRequired] string Description, [property: JsonRequired] DateTimeOffset StartTime, [property: JsonRequired] DateTimeOffset EndTime, [property: JsonRequired] DateTimeOffset UpdatedAt, [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] ImmutableList<WorkoutExerciseResponse> Exercises) : IHevyResponse
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Id) || StartTime == default || EndTime == default || UpdatedAt == default || CreatedAt == default) throw new JsonException();
    foreach (var exercise in Exercises)
    {
      if (exercise is null) throw new JsonException();
      exercise.Validate();
    }
  }

  internal Workout ToDomain() => new(Id, Title, RoutineId, Description, StartTime, EndTime, UpdatedAt, CreatedAt, Exercises.Select(static exercise => exercise.ToDomain()).ToImmutableList());
}
