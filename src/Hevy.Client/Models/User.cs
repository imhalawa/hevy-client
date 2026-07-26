using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record UserInfo(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Url);

public sealed record UserInfoResponse([property: JsonRequired] UserInfo Data);
