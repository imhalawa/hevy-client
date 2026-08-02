using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record RoutineFolderPageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<RoutineFolderResponse> RoutineFolders) : IHevyResponse
{
  public void Validate()
  {
    foreach (var folder in RoutineFolders)
    {
      if (folder is null) throw new JsonException();
      folder.Validate();
    }
  }
}
