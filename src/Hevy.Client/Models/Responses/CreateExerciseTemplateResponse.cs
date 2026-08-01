using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record CreateExerciseTemplateResponse([property: JsonRequired] int Id) : IHevyResponse
{
  public void Validate()
  {
    if (Id <= 0) throw new JsonException();
  }
}
