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

  public static HevyClientOptions FromEnvironment()
  {
    var apiKey = Environment.GetEnvironmentVariable("HEVY_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      throw new InvalidOperationException("HEVY_API_KEY is required.");
    }

    return new HevyClientOptions(apiKey);
  }

  public override string ToString() => "HevyClientOptions { ApiKey = [REDACTED] }";
}
