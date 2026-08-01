using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record BodyMeasurementPageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<BodyMeasurementResponse> BodyMeasurements);
