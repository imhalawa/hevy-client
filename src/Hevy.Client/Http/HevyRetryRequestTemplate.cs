using System.Security.Cryptography;

namespace Hevy.Client.Http;

internal sealed class HevyRetryRequestTemplate : IDisposable
{
  private readonly HttpMethod method;
  private readonly Uri? requestUri;
  private readonly Version version;
  private readonly HttpVersionPolicy versionPolicy;
  private readonly ImmutableList<KeyValuePair<string, string[]>> headers;
  private readonly ImmutableList<KeyValuePair<string, string[]>> contentHeaders;
  private byte[]? content;
  private readonly bool retrySafeMutation;
  private readonly DateTimeOffset? deadline;

  private HevyRetryRequestTemplate(HttpRequestMessage request, byte[]? content)
  {
    method = request.Method;
    requestUri = request.RequestUri;
    version = request.Version;
    versionPolicy = request.VersionPolicy;
    headers = request.Headers.Select(header => new KeyValuePair<string, string[]>(header.Key, header.Value.ToArray())).ToImmutableList();
    contentHeaders = request.Content?.Headers.Select(header => new KeyValuePair<string, string[]>(header.Key, header.Value.ToArray())).ToImmutableList() ?? [];
    this.content = content;
    retrySafeMutation = request.Options.TryGetValue(HevyRetryHandler.RetrySafeMutation, out var safe) && safe;
    deadline = request.Options.TryGetValue(HevyRetryHandler.RetryDeadline, out var operationDeadline) ? operationDeadline : null;
  }

  internal static async Task<HevyRetryRequestTemplate> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
      new(request, request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken));

  internal HttpRequestMessage CreateRequest()
  {
    var request = new HttpRequestMessage(method, requestUri) { Version = version, VersionPolicy = versionPolicy };
    foreach (var header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
    if (content is not null)
    {
      request.Content = new ByteArrayContent(content);
      foreach (var header in contentHeaders) request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
    }
    if (retrySafeMutation) request.Options.Set(HevyRetryHandler.RetrySafeMutation, true);
    if (deadline is DateTimeOffset operationDeadline) request.Options.Set(HevyRetryHandler.RetryDeadline, operationDeadline);
    return request;
  }

  public void Dispose()
  {
    if (content is null) return;
    CryptographicOperations.ZeroMemory(content);
    content = null;
  }
}
