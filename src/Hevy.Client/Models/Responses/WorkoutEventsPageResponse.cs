using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record WorkoutEventsPageResponse([property: JsonRequired] int Page, [property: JsonRequired] int PageCount, [property: JsonRequired] ImmutableList<WorkoutEventResponse> Events) : IHevyResponse
{
  public void Validate()
  {
    foreach (var workoutEvent in Events)
    {
      if (workoutEvent is null) throw new JsonException();
      workoutEvent.Validate();
    }
  }
}
