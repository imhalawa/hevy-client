using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record RoutineFolderPageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<RoutineFolderResponse> RoutineFolders);
