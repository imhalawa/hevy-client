using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record WorkoutPageResponse([property: JsonRequired] int Page, [property: JsonRequired] int PageCount, [property: JsonRequired] ImmutableList<WorkoutResponse> Workouts) : IHevyResponse
{
  public void Validate()
  {
    foreach (var workout in Workouts)
    {
      if (workout is null) throw new JsonException();
      workout.Validate();
    }
  }
}
