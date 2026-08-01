using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record RoutineFolderPageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<RoutineFolderResponse> RoutineFolders);
