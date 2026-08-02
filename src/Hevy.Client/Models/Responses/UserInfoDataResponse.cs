using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record UserInfoDataResponse([property: JsonRequired] string Id, [property: JsonRequired] string Name, [property: JsonRequired] string Url)
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Id)) throw new JsonException();
  }

  internal UserInfo ToDomain() => new(Id, Name, Url);
}
