using System.Text.Json;

namespace Hevy.Client.Http;

internal readonly record struct BufferedJsonToken(JsonTokenType Type, string? Text, byte[]? RawValue)
{
  internal static BufferedJsonToken Create(ref Utf8JsonReader reader) => reader.TokenType switch
  {
    JsonTokenType.PropertyName or JsonTokenType.String => new(reader.TokenType, reader.GetString(), null),
    JsonTokenType.Number => new(reader.TokenType, null, reader.ValueSpan.ToArray()),
    _ => new(reader.TokenType, null, null),
  };
}
