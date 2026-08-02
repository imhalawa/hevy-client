using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record BodyMeasurementPageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<BodyMeasurementResponse> BodyMeasurements) : IHevyResponse
{
  public void Validate()
  {
    foreach (var measurement in BodyMeasurements)
    {
      if (measurement is null) throw new JsonException();
      measurement.Validate();
    }
  }
}
