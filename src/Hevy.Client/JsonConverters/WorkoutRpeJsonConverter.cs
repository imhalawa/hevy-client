using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed class WorkoutRpeJsonConverter : JsonConverter<WorkoutRpe>
{
  public override WorkoutRpe Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
      new(reader.GetDecimal());

  public override void Write(Utf8JsonWriter writer, WorkoutRpe value, JsonSerializerOptions options)
  {
    if (!WorkoutRpe.IsValid(value.Value))
    {
      throw new JsonException("RPE is invalid.");
    }
    writer.WriteNumberValue(value.Value);
  }
}
