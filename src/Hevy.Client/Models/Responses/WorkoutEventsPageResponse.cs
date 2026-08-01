using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record WorkoutEventsPageResponse([property: JsonRequired] int Page, [property: JsonRequired] int PageCount, [property: JsonRequired] ImmutableList<WorkoutEventResponse> Events);
