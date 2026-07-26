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
      ExerciseHistoryWindowRequest request,
      JsonTypeInfo<ExerciseHistoryEntry> entryTypeInfo,
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
      if (history.ScannedItemCount == ExerciseHistoryWindowRequest.MaximumScannedItems)
      {
        return history.Truncated(ExerciseHistoryWindow.ItemSafetyCap);
      }

      using var payload = new MemoryStream();
      using (var writer = new Utf8JsonWriter(payload))
      {
        await ConsumeValueAsync(reader, token, writer, cancellationToken).ConfigureAwait(false);
      }

      var entry = JsonSerializer.Deserialize(payload.ToArray(), history.EntryTypeInfo) ?? throw new JsonException();
      var truncated = history.Add(entry);
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
      if (token.Type is JsonTokenType.StartArray or JsonTokenType.StartObject)
      {
        depth++;
      }
      else if (token.Type is JsonTokenType.EndArray or JsonTokenType.EndObject)
      {
        depth--;
      }
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

  private sealed class HistoryReadState(
      ExerciseHistoryWindowRequest request,
      JsonTypeInfo<ExerciseHistoryEntry> entryTypeInfo)
  {
    private readonly List<ExerciseHistoryEntry> items = new(request.Limit);
    private int eligibleItemCount;

    internal int ScannedItemCount { get; private set; }
    internal JsonTypeInfo<ExerciseHistoryEntry> EntryTypeInfo { get; } = entryTypeInfo;

    internal ExerciseHistoryWindow? Add(ExerciseHistoryEntry entry)
    {
      ScannedItemCount++;
      if (!IsEligible(entry, request)) return null;
      if (eligibleItemCount++ < request.Offset) return null;
      if (items.Count < request.Limit)
      {
        items.Add(entry);
        return null;
      }

      return Truncated(null);
    }

    internal ExerciseHistoryWindow Complete() =>
        new(items.AsReadOnly(), false, ScannedItemCount);

    internal ExerciseHistoryWindow Truncated(string? reason) =>
        new(items.AsReadOnly(), true, ScannedItemCount, reason);
  }

  private static bool IsEligible(ExerciseHistoryEntry entry, ExerciseHistoryWindowRequest request) =>
      (request.EligibleStartTime is null || entry.WorkoutStartTime >= request.EligibleStartTime) &&
      (request.EligibleEndTime is null || entry.WorkoutStartTime < request.EligibleEndTime);

  private readonly record struct BufferedJsonToken(JsonTokenType Type, string? Text, byte[]? RawValue)
  {
    internal static BufferedJsonToken Create(ref Utf8JsonReader reader) => reader.TokenType switch
    {
      JsonTokenType.PropertyName or JsonTokenType.String => new(reader.TokenType, reader.GetString(), null),
      JsonTokenType.Number => new(reader.TokenType, null, reader.ValueSpan.ToArray()),
      _ => new(reader.TokenType, null, null),
    };
  }

  private sealed class IncrementalJsonReader(Stream stream, long maximumBytes)
  {
    private const int BufferSize = 4_096;
    private readonly Stream stream = stream;
    private readonly long maximumBytes = maximumBytes;
    private byte[] buffer = new byte[(int)Math.Min(BufferSize, maximumBytes)];
    private JsonReaderState state;
    private int position;
    private int length;
    private long bytesRead;
    private bool isFinalBlock;

    internal async ValueTask<BufferedJsonToken?> ReadTokenAsync(CancellationToken cancellationToken)
    {
      while (true)
      {
        if (TryReadToken(out var token)) return token;
        if (isFinalBlock) return null;
        await FillBufferAsync(cancellationToken).ConfigureAwait(false);
      }
    }

    private bool TryReadToken(out BufferedJsonToken token)
    {
      var reader = new Utf8JsonReader(buffer.AsSpan(position, length - position), isFinalBlock, state);
      if (!reader.Read())
      {
        Advance(ref reader);
        token = default;
        return false;
      }

      token = BufferedJsonToken.Create(ref reader);
      Advance(ref reader);
      return true;
    }

    private void Advance(ref Utf8JsonReader reader)
    {
      position += checked((int)reader.BytesConsumed);
      state = reader.CurrentState;
    }

    private async ValueTask FillBufferAsync(CancellationToken cancellationToken)
    {
      CompactBuffer();
      if (length == buffer.Length) GrowBuffer();
      if (bytesRead >= maximumBytes) throw new ResponseByteLimitExceededException();

      var requested = (int)Math.Min(buffer.Length - length, maximumBytes - bytesRead);
      var read = await stream.ReadAsync(buffer.AsMemory(length, requested), cancellationToken).ConfigureAwait(false);
      if (read == 0)
      {
        isFinalBlock = true;
        return;
      }

      length += read;
      bytesRead += read;
    }

    private void CompactBuffer()
    {
      if (position == 0) return;
      buffer.AsSpan(position, length - position).CopyTo(buffer);
      length -= position;
      position = 0;
    }

    private void GrowBuffer()
    {
      var maximumCapacity = Math.Min(maximumBytes, int.MaxValue);
      if (buffer.Length >= maximumCapacity) throw new ResponseByteLimitExceededException();
      var newLength = (int)Math.Min((long)buffer.Length * 2, maximumCapacity);
      Array.Resize(ref buffer, newLength);
    }
  }

  private sealed class ResponseByteLimitExceededException : Exception;
}
