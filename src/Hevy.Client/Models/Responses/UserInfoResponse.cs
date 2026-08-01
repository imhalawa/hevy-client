using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record UserInfoResponse([property: JsonRequired] UserInfoDataResponse Data) : IHevyResponse
{
  public void Validate() => Data.Validate();
}
