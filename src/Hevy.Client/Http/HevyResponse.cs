using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hevy.Client.Errors;

namespace Hevy.Client.Http;

internal static class HevyResponse
{
  public static void EnsureSuccess(HttpResponseMessage response)
  {
    ArgumentNullException.ThrowIfNull(response);
    if (!response.IsSuccessStatusCode)
    {
      throw CreateException(response.StatusCode);
    }
  }

  public static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
  {
    EnsureSuccess(response);

    try
    {
      await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      var value = await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken);
      return value ?? throw UnexpectedResponse(response.StatusCode);
    }
    catch (JsonException)
    {
      throw UnexpectedResponse(response.StatusCode);
    }
  }

  public static HevyException UnexpectedResponse(HttpStatusCode statusCode) =>
      new("unexpected_response", "The Hevy API returned an invalid response.", false, statusCode);

  private static HevyException CreateException(HttpStatusCode statusCode) => statusCode switch
  {
    HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => new HevyException("validation", "The Hevy API rejected the request.", false, statusCode),
    HttpStatusCode.Unauthorized => new HevyException("authentication", "The Hevy API rejected the credentials.", false, statusCode),
    HttpStatusCode.Forbidden => new HevyException("authorization", "The Hevy API denied access to this resource.", false, statusCode),
    HttpStatusCode.NotFound => new HevyException("not_found", "The requested Hevy resource was not found.", false, statusCode),
    HttpStatusCode.Conflict => new HevyException("conflict", "The Hevy API reported a conflicting change.", false, statusCode),
    HttpStatusCode.TooManyRequests => new HevyException("rate_limited", "The Hevy API rate limit was reached.", true, statusCode),
    _ when (int)statusCode >= 500 => new HevyException("transient_upstream", "The Hevy API is temporarily unavailable.", true, statusCode),
    _ => new HevyException("unexpected_response", "The Hevy API returned an unexpected response.", false, statusCode),
  };
}
