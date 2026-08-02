using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record RoutineFolderResponse([property: JsonRequired] long Id, [property: JsonRequired] int Index, [property: JsonRequired] string Title, [property: JsonRequired] DateTimeOffset UpdatedAt, [property: JsonRequired] DateTimeOffset CreatedAt) : IHevyResponse
{
  public void Validate()
  {
    var hasRequiredFields = Id > 0 && UpdatedAt != default && CreatedAt != default;
    if (!hasRequiredFields) throw new JsonException();
  }

  internal RoutineFolder ToDomain() => new(Id, Index, Title, UpdatedAt, CreatedAt);
}
