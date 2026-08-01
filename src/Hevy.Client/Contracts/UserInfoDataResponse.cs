using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record UserInfoDataResponse([property: JsonRequired] string Id, [property: JsonRequired] string Name, [property: JsonRequired] string Url);
