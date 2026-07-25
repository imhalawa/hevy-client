namespace Hevy.Client.Http;

public sealed class HevyAuthenticationHandler : DelegatingHandler
{
  private readonly string apiKey;

  public HevyAuthenticationHandler(HevyClientOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);
    apiKey = options.ApiKey;
  }

  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    request.Headers.Remove("api-key");
    request.Headers.TryAddWithoutValidation("api-key", apiKey);
    return base.SendAsync(request, cancellationToken);
  }
}
