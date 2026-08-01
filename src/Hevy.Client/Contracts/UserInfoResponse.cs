using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record UserInfoResponse([property: JsonRequired] UserInfoDataResponse Data);
