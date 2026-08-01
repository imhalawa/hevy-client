using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Hevy.Mcp.Configuration;

public sealed class HevyMcpOptions
{
  internal string ApiKey { get; }

  internal string? McpAuthToken { get; }

  public HevyMcpTransport Transport { get; }

  public bool ReadOnly { get; }

  public LogLevel LogLevel { get; }

  private HevyMcpOptions(string apiKey, HevyMcpTransport transport, bool readOnly, LogLevel logLevel, string? mcpAuthToken)
  {
    ApiKey = apiKey;
    Transport = transport;
    ReadOnly = readOnly;
    LogLevel = logLevel;
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
    var logLevel = ParseLogLevel(getEnvironmentVariable("HEVY_LOG_LEVEL"));
    var authToken = getEnvironmentVariable("MCP_AUTH_TOKEN");

    if (transport is HevyMcpTransport.Http)
    {
      if (string.IsNullOrWhiteSpace(authToken))
      {
        throw new InvalidOperationException("MCP_AUTH_TOKEN is required for HTTP transport.");
      }

      if (!BearerToken.IsValidToken68(authToken))
      {
        throw new InvalidOperationException("MCP_AUTH_TOKEN must use Bearer token68 syntax.");
      }

      if (FixedTimeEquals(apiKey, authToken))
      {
        throw new InvalidOperationException("MCP_AUTH_TOKEN must be distinct from HEVY_API_KEY.");
      }
    }

    return new HevyMcpOptions(apiKey, transport, readOnly, logLevel, transport is HevyMcpTransport.Http ? authToken : null);
  }

  public override string ToString() => $"HevyMcpOptions {{ Transport = {Transport}, ReadOnly = {ReadOnly}, LogLevel = {LogLevel} }}";

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

  private static LogLevel ParseLogLevel(string? value) => value switch
  {
    null => LogLevel.None,
    "Trace" => LogLevel.Trace,
    "Debug" => LogLevel.Debug,
    "Information" => LogLevel.Information,
    "Warning" => LogLevel.Warning,
    "Error" => LogLevel.Error,
    "Critical" => LogLevel.Critical,
    "None" => LogLevel.None,
    _ => throw new InvalidOperationException("HEVY_LOG_LEVEL must be one of Trace, Debug, Information, Warning, Error, Critical, or None."),
  };

  private static bool FixedTimeEquals(string left, string right) =>
      CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
