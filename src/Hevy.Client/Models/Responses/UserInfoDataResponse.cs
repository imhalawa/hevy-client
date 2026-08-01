using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record UserInfoDataResponse([property: JsonRequired] string Id, [property: JsonRequired] string Name, [property: JsonRequired] string Url)
{
  internal UserInfo ToDomain() => new(Id, Name, Url);
}
