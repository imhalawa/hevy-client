using System.Text.Json;

namespace Hevy.Client.Http;

internal sealed class IncrementalJsonReader(Stream stream, long maximumBytes)
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
