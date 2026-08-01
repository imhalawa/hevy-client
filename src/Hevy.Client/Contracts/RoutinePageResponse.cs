using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record RoutinePageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<RoutineResponse> Routines);
