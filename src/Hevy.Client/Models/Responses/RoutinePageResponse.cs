using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record RoutinePageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<RoutineResponse> Routines) : IHevyResponse
{
  public void Validate()
  {
    foreach (var routine in Routines)
    {
      if (routine is null) throw new JsonException();
      routine.Validate();
    }
  }
}
