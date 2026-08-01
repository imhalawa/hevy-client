using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record WorkoutCountResponse([property: JsonRequired] int WorkoutCount) : IHevyResponse
{
  public void Validate()
  {
    if (WorkoutCount < 0) throw new JsonException();
  }
}
