using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hevy.Client.Models;

namespace Hevy.Client.Http;

internal static class ExerciseHistoryStreamReader
{
  private static readonly byte[] ExerciseHistoryPropertyName = "exercise_history"u8.ToArray();

  internal static async Task<ExerciseHistoryWindow> ReadAsync(
      Stream stream,
      ExerciseHistoryWindowRequest request,
      JsonTypeInfo<ExerciseHistoryEntry> entryTypeInfo,
      long maximumResponseBytes,
      HttpStatusCode statusCode,
      CancellationToken cancellationToken)
  {
    var reader = new BoundedByteReader(stream, maximumResponseBytes);
    var items = new List<ExerciseHistoryEntry>(request.Limit);
    var eligibleCount = 0;
    var scannedCount = 0;

    try
    {
      await ReadEnvelopeStartAsync(reader, cancellationToken).ConfigureAwait(false);
      var first = true;
      while (true)
      {
        var token = await reader.ReadNonWhitespaceAsync(cancellationToken).ConfigureAwait(false);
        if (token == ']')
        {
          return new ExerciseHistoryWindow(items.AsReadOnly(), false, scannedCount);
        }
        if (!first)
        {
          if (token != ',') throw new JsonException();
          token = await reader.ReadNonWhitespaceAsync(cancellationToken).ConfigureAwait(false);
        }
        first = false;
        if (token != '{') throw new JsonException();

        if (scannedCount == ExerciseHistoryWindowRequest.MaximumScannedItems)
        {
          return new ExerciseHistoryWindow(items.AsReadOnly(), true, scannedCount, ExerciseHistoryWindow.ItemSafetyCap);
        }

        var payload = await ReadObjectAsync(reader, cancellationToken).ConfigureAwait(false);
        scannedCount++;
        var entry = JsonSerializer.Deserialize(payload, entryTypeInfo) ?? throw new JsonException();
        if (!IsEligible(entry, request)) continue;

        if (eligibleCount++ < request.Offset) continue;
        if (items.Count < request.Limit)
        {
          items.Add(entry);
          continue;
        }

        return new ExerciseHistoryWindow(items.AsReadOnly(), true, scannedCount);
      }
    }
    catch (ResponseByteLimitExceededException)
    {
      return new ExerciseHistoryWindow(items.AsReadOnly(), true, scannedCount, ExerciseHistoryWindow.ByteSafetyCap);
    }
    catch (JsonException)
    {
      throw HevyResponse.UnexpectedResponse(statusCode);
    }
    catch (EndOfStreamException)
    {
      throw HevyResponse.UnexpectedResponse(statusCode);
    }
  }

  private static bool IsEligible(ExerciseHistoryEntry entry, ExerciseHistoryWindowRequest request) =>
      (request.EligibleStartTime is null || entry.WorkoutStartTime >= request.EligibleStartTime) &&
      (request.EligibleEndTime is null || entry.WorkoutStartTime < request.EligibleEndTime);

  private static async Task ReadEnvelopeStartAsync(BoundedByteReader reader, CancellationToken cancellationToken)
  {
    if (await reader.ReadNonWhitespaceAsync(cancellationToken).ConfigureAwait(false) != '{' ||
        await reader.ReadNonWhitespaceAsync(cancellationToken).ConfigureAwait(false) != '"')
    {
      throw new JsonException();
    }

    for (var index = 0; index < ExerciseHistoryPropertyName.Length; index++)
    {
      if (await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false) != ExerciseHistoryPropertyName[index])
      {
        throw new JsonException();
      }
    }

    if (await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false) != '"' ||
        await reader.ReadNonWhitespaceAsync(cancellationToken).ConfigureAwait(false) != ':' ||
        await reader.ReadNonWhitespaceAsync(cancellationToken).ConfigureAwait(false) != '[')
    {
      throw new JsonException();
    }
  }

  private static async Task<byte[]> ReadObjectAsync(BoundedByteReader reader, CancellationToken cancellationToken)
  {
    using var payload = new MemoryStream();
    payload.WriteByte((byte)'{');
    var depth = 1;
    var inString = false;
    var escaped = false;
    while (depth > 0)
    {
      var next = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
      payload.WriteByte((byte)next);
      if (inString)
      {
        if (escaped)
        {
          escaped = false;
        }
        else if (next == '\\')
        {
          escaped = true;
        }
        else if (next == '"')
        {
          inString = false;
        }
        continue;
      }

      if (next == '"')
      {
        inString = true;
      }
      else if (next is '{' or '[')
      {
        depth++;
      }
      else if (next is '}' or ']')
      {
        depth--;
      }
    }
    return payload.ToArray();
  }

  private sealed class BoundedByteReader(Stream stream, long maximumBytes)
  {
    private readonly byte[] buffer = new byte[4_096];
    private int position;
    private int length;
    private long bytesRead;

    internal async ValueTask<int> ReadNonWhitespaceAsync(CancellationToken cancellationToken)
    {
      while (true)
      {
        var value = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        if (value is not (' ' or '\t' or '\r' or '\n')) return value;
      }
    }

    internal async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken)
    {
      if (position < length) return buffer[position++];
      if (bytesRead >= maximumBytes) throw new ResponseByteLimitExceededException();
      var requested = (int)Math.Min(buffer.Length, maximumBytes - bytesRead);
      length = await stream.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
      position = 0;
      if (length == 0) throw new EndOfStreamException();
      bytesRead += length;
      return buffer[position++];
    }
  }

  private sealed class ResponseByteLimitExceededException : Exception;
}
