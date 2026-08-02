using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hevy.Client.Models;

namespace Hevy.Client.Http;

internal static class ExerciseHistoryStreamReader
{
  private const string ExerciseHistoryPropertyName = "exercise_history";

  internal static async Task<ExerciseHistoryWindow> ReadAsync(
      Stream stream,
      ExerciseHistoryQuery request,
      JsonTypeInfo<ExerciseHistoryEntryResponse> entryTypeInfo,
      long maximumResponseBytes,
      HttpStatusCode statusCode,
      CancellationToken cancellationToken)
  {
    var reader = new IncrementalJsonReader(stream, maximumResponseBytes);
    var history = new HistoryReadState(request, entryTypeInfo);

    try
    {
      if ((await ReadRequiredTokenAsync(reader, cancellationToken).ConfigureAwait(false)).Type != JsonTokenType.StartObject)
      {
        throw new JsonException();
      }

      var foundHistory = false;
      while (true)
      {
        var token = await ReadRequiredTokenAsync(reader, cancellationToken).ConfigureAwait(false);
        if (token.Type == JsonTokenType.EndObject) break;
        if (token.Type != JsonTokenType.PropertyName) throw new JsonException();

        var value = await ReadRequiredTokenAsync(reader, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(token.Text, ExerciseHistoryPropertyName, StringComparison.Ordinal))
        {
          await ConsumeValueAsync(reader, value, null, cancellationToken).ConfigureAwait(false);
          continue;
        }

        if (foundHistory || value.Type != JsonTokenType.StartArray) throw new JsonException();
        foundHistory = true;
        var truncated = await ReadHistoryArrayAsync(reader, history, cancellationToken).ConfigureAwait(false);
        if (truncated is not null) return truncated;
      }

      if (!foundHistory || await reader.ReadTokenAsync(cancellationToken).ConfigureAwait(false) is not null)
      {
        throw new JsonException();
      }

      return history.Complete();
    }
    catch (ResponseByteLimitExceededException)
    {
      return history.Truncated(ExerciseHistoryWindow.ByteSafetyCap);
    }
    catch (JsonException)
    {
      throw HevyResponse.UnexpectedResponse(statusCode);
    }
  }

  private static async Task<ExerciseHistoryWindow?> ReadHistoryArrayAsync(
      IncrementalJsonReader reader,
      HistoryReadState history,
      CancellationToken cancellationToken)
  {
    while (true)
    {
      var token = await ReadRequiredTokenAsync(reader, cancellationToken).ConfigureAwait(false);
      if (token.Type == JsonTokenType.EndArray) return null;
      if (token.Type != JsonTokenType.StartObject) throw new JsonException();
      if (history.ScannedItemCount == ExerciseHistoryQuery.MaximumScannedItems)
      {
        return history.Truncated(ExerciseHistoryWindow.ItemSafetyCap);
      }

      using var payload = new MemoryStream();
      using (var writer = new Utf8JsonWriter(payload))
      {
        await ConsumeValueAsync(reader, token, writer, cancellationToken).ConfigureAwait(false);
      }

      var response = JsonSerializer.Deserialize(payload.ToArray(), history.EntryTypeInfo) ?? throw new JsonException();
      response.Validate();
      var truncated = history.Add(response.ToDomain());
      if (truncated is not null) return truncated;
    }
  }

  private static async Task ConsumeValueAsync(
      IncrementalJsonReader reader,
      BufferedJsonToken firstToken,
      Utf8JsonWriter? writer,
      CancellationToken cancellationToken)
  {
    var depth = firstToken.Type switch
    {
      JsonTokenType.StartArray or JsonTokenType.StartObject => 1,
      JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null => 0,
      _ => throw new JsonException(),
    };
    WriteToken(writer, firstToken);

    while (depth > 0)
    {
      var token = await ReadRequiredTokenAsync(reader, cancellationToken).ConfigureAwait(false);
      WriteToken(writer, token);
      depth += token.Type switch
      {
        JsonTokenType.StartArray or JsonTokenType.StartObject => 1,
        JsonTokenType.EndArray or JsonTokenType.EndObject => -1,
        _ => 0,
      };
    }

    writer?.Flush();
  }

  private static void WriteToken(Utf8JsonWriter? writer, BufferedJsonToken token)
  {
    if (writer is null) return;
    switch (token.Type)
    {
      case JsonTokenType.StartObject:
        writer.WriteStartObject();
        break;
      case JsonTokenType.EndObject:
        writer.WriteEndObject();
        break;
      case JsonTokenType.StartArray:
        writer.WriteStartArray();
        break;
      case JsonTokenType.EndArray:
        writer.WriteEndArray();
        break;
      case JsonTokenType.PropertyName:
        writer.WritePropertyName(token.Text ?? throw new JsonException());
        break;
      case JsonTokenType.String:
        writer.WriteStringValue(token.Text);
        break;
      case JsonTokenType.Number:
        writer.WriteRawValue(token.RawValue ?? throw new JsonException(), skipInputValidation: true);
        break;
      case JsonTokenType.True:
        writer.WriteBooleanValue(true);
        break;
      case JsonTokenType.False:
        writer.WriteBooleanValue(false);
        break;
      case JsonTokenType.Null:
        writer.WriteNullValue();
        break;
      default:
        throw new JsonException();
    }
  }

  private static async ValueTask<BufferedJsonToken> ReadRequiredTokenAsync(
      IncrementalJsonReader reader,
      CancellationToken cancellationToken) =>
      await reader.ReadTokenAsync(cancellationToken).ConfigureAwait(false) ?? throw new JsonException();

}
