using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed class SetTypeApiJsonConverter : JsonConverter<SetTypeApi>
{
  public override SetTypeApi Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    return reader.GetString() switch
    {
      "warmup" => SetTypeApi.Warmup,
      "normal" => SetTypeApi.Normal,
      "failure" => SetTypeApi.Failure,
      "dropset" => SetTypeApi.Dropset,
      _ => throw new JsonException("Set type is invalid."),
    };
  }

  public override void Write(Utf8JsonWriter writer, SetTypeApi value, JsonSerializerOptions options)
  {
    writer.WriteStringValue(value switch
    {
      SetTypeApi.Warmup => "warmup",
      SetTypeApi.Normal => "normal",
      SetTypeApi.Failure => "failure",
      SetTypeApi.Dropset => "dropset",
      _ => throw new JsonException("Set type is invalid."),
    });
  }
}
