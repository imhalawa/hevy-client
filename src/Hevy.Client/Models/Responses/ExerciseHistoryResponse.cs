using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record ExerciseHistoryResponse([property: JsonRequired] ImmutableList<ExerciseHistoryEntryResponse> ExerciseHistory) : IHevyResponse
{
  public void Validate()
  {
    foreach (var entry in ExerciseHistory)
    {
      if (entry is null) throw new JsonException();
      entry.Validate();
    }
  }
}
