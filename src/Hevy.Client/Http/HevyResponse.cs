using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hevy.Core.Exceptions;
using Hevy.Client.Models;

namespace Hevy.Client.Http;

internal static class HevyResponse
{
  internal const int MaximumResponseBytes = 4 * 1024 * 1024;

  public static void EnsureSuccess(HttpResponseMessage response)
  {
    if (!response.IsSuccessStatusCode)
    {
      throw CreateException(response);
    }
  }

  public static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
  {
    EnsureSuccess(response);

    try
    {
      if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
      {
        throw UnexpectedResponse(response.StatusCode);
      }

      await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      using var payload = new MemoryStream();
      var buffer = new byte[81_920];
      while (true)
      {
        var remaining = MaximumResponseBytes + 1L - payload.Length;
        if (remaining <= 0) throw UnexpectedResponse(response.StatusCode);
        var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
        if (read == 0) break;
        payload.Write(buffer, 0, read);
      }

      if (payload.Length > MaximumResponseBytes) throw UnexpectedResponse(response.StatusCode);
      var value = JsonSerializer.Deserialize(payload.GetBuffer().AsSpan(0, checked((int)payload.Length)), jsonTypeInfo)
          ?? throw UnexpectedResponse(response.StatusCode);
      if (value is IHevyResponse contract) contract.Validate();
      return value;
    }
    catch (JsonException)
    {
      throw UnexpectedResponse(response.StatusCode);
    }
    catch (NotSupportedException)
    {
      throw UnexpectedResponse(response.StatusCode);
    }
  }

  public static HevyException UnexpectedResponse(HttpStatusCode statusCode) =>
      new("unexpected_response", "The Hevy API returned an invalid response.", false, statusCode);

  private static HevyException CreateException(HttpResponseMessage response)
  {
    var statusCode = response.StatusCode;
    var requestId = SafeRequestId(response);
    return statusCode switch
    {
      HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => new HevyException("validation", "The Hevy API rejected the request.", false, statusCode, requestId),
      HttpStatusCode.Unauthorized => new HevyException("authentication", "The Hevy API rejected the credentials.", false, statusCode, requestId),
      HttpStatusCode.Forbidden => new HevyException("authorization", "The Hevy API denied access to this resource.", false, statusCode, requestId),
      HttpStatusCode.NotFound => new HevyException("not_found", "The requested Hevy resource was not found.", false, statusCode, requestId),
      HttpStatusCode.Conflict => new HevyException("conflict", "The Hevy API reported a conflicting change.", false, statusCode, requestId),
      HttpStatusCode.TooManyRequests => new HevyException("rate_limited", "The Hevy API rate limit was reached.", true, statusCode, requestId),
      _ when (int)statusCode >= 500 => new HevyException("transient_upstream", "The Hevy API is temporarily unavailable.", true, statusCode, requestId),
      _ => new HevyException("unexpected_response", "The Hevy API returned an unexpected response.", false, statusCode, requestId),
    };
  }

  internal static string? SafeRequestId(HttpResponseMessage response)
  {
    if (!response.Headers.TryGetValues("X-Request-Id", out var values)) return null;
    using var enumerator = values.GetEnumerator();
    if (!enumerator.MoveNext()) return null;
    var value = enumerator.Current;
    if (enumerator.MoveNext()) return null;
    return value is { Length: >= 1 and <= 128 } && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-')
        ? value
        : null;
  }
}
