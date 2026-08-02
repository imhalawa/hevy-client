using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public enum SetTypeApi
{
  [JsonStringEnumMemberName("warmup")]
  Warmup,
  [JsonStringEnumMemberName("normal")]
  Normal,
  [JsonStringEnumMemberName("failure")]
  Failure,
  [JsonStringEnumMemberName("dropset")]
  Dropset
}
