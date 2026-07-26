using System.Security.Cryptography;
using System.Text;

namespace Hevy.Mcp.Configuration;

public enum HevyMcpTransport
{
  Stdio,
  Http,
}

public sealed class HevyMcpOptions
{
  internal string? McpAuthToken { get; }

  public HevyMcpTransport Transport { get; }

  public bool ReadOnly { get; }

  private HevyMcpOptions(HevyMcpTransport transport, bool readOnly, string? mcpAuthToken)
  {
    Transport = transport;
    ReadOnly = readOnly;
    McpAuthToken = mcpAuthToken;
  }

  public static HevyMcpOptions FromEnvironment() => FromEnvironment(Environment.GetEnvironmentVariable);

  internal static HevyMcpOptions FromEnvironment(Func<string, string?> getEnvironmentVariable)
  {
    ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

    var apiKey = getEnvironmentVariable("HEVY_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      throw new InvalidOperationException("HEVY_API_KEY is required.");
    }

    var transport = ParseTransport(getEnvironmentVariable("HEVY_MCP_TRANSPORT"));
    var readOnly = ParseReadOnly(getEnvironmentVariable("HEVY_READ_ONLY"));
    var authToken = getEnvironmentVariable("MCP_AUTH_TOKEN");

    if (transport is HevyMcpTransport.Http)
    {
      if (string.IsNullOrWhiteSpace(authToken))
      {
        throw new InvalidOperationException("MCP_AUTH_TOKEN is required for HTTP transport.");
      }

      if (FixedTimeEquals(apiKey, authToken))
      {
        throw new InvalidOperationException("MCP_AUTH_TOKEN must be distinct from HEVY_API_KEY.");
      }
    }

    return new HevyMcpOptions(transport, readOnly, transport is HevyMcpTransport.Http ? authToken : null);
  }

  public override string ToString() => $"HevyMcpOptions {{ Transport = {Transport}, ReadOnly = {ReadOnly} }}";

  private static HevyMcpTransport ParseTransport(string? value) => value switch
  {
    null => HevyMcpTransport.Stdio,
    "stdio" => HevyMcpTransport.Stdio,
    "http" => HevyMcpTransport.Http,
    _ => throw new InvalidOperationException("HEVY_MCP_TRANSPORT must be either 'stdio' or 'http'."),
  };

  private static bool ParseReadOnly(string? value) => value switch
  {
    null => false,
    "true" => true,
    "false" => false,
    _ => throw new InvalidOperationException("HEVY_READ_ONLY must be either 'true' or 'false'."),
  };

  private static bool FixedTimeEquals(string left, string right) =>
      CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
