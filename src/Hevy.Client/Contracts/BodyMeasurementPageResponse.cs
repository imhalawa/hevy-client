using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record BodyMeasurementPageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<BodyMeasurementResponse> BodyMeasurements);
