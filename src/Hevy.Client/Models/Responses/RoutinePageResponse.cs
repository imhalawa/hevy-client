using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record RoutinePageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<RoutineResponse> Routines);
