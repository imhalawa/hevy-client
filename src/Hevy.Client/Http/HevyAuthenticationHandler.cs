namespace Hevy.Client.Http;

public sealed class HevyAuthenticationHandler : DelegatingHandler
{
  internal static readonly Uri ApiOrigin = new("https://api.hevyapp.com/", UriKind.Absolute);
  private readonly string apiKey;

  public HevyAuthenticationHandler(HevyClientOptions options)
  {
    apiKey = options.ApiKey;
  }

  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    EnsureSafeTarget(request.RequestUri);
    request.Headers.Remove("api-key");
    request.Headers.TryAddWithoutValidation("api-key", apiKey);
    return base.SendAsync(request, cancellationToken);
  }

  internal static void EnsureSafeTarget(Uri? requestUri)
  {
    if (requestUri is null ||
        !requestUri.IsAbsoluteUri ||
        !string.Equals(requestUri.Scheme, ApiOrigin.Scheme, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(requestUri.Host, ApiOrigin.Host, StringComparison.OrdinalIgnoreCase) ||
        requestUri.Port != ApiOrigin.Port ||
        !string.IsNullOrEmpty(requestUri.UserInfo))
    {
      throw new InvalidOperationException("Authenticated Hevy requests are restricted to the official API origin.");
    }
  }
}
