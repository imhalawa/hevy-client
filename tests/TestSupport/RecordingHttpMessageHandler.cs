using System.Net;

namespace TestSupport;

public sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
  private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory;

  public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory)
  {
    ArgumentNullException.ThrowIfNull(responseFactory);
    this.responseFactory = responseFactory;
  }

  public List<RecordedHttpRequest> Requests { get; } = [];

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
    Requests.Add(new RecordedHttpRequest(
        request.Method,
        request.RequestUri,
        request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
        body,
        cancellationToken));

    return responseFactory(request, cancellationToken);
  }

  public static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
  {
    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
  };
}

public sealed record RecordedHttpRequest(
    HttpMethod Method,
    Uri? RequestUri,
    IReadOnlyDictionary<string, string[]> Headers,
    string? Body,
    CancellationToken CancellationToken);
