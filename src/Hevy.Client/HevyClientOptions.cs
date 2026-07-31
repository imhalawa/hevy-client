namespace Hevy.Client;

public sealed class HevyClientOptions
{
  internal string ApiKey { get; }

  internal HevyClientOptions(string apiKey)
  {
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      throw new ArgumentException("A non-empty Hevy API key is required.", nameof(apiKey));
    }

    ApiKey = apiKey;
  }

  public override string ToString() => "HevyClientOptions { ApiKey = [REDACTED] }";
}
