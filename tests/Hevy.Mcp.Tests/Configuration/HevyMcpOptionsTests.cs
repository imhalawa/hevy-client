using Hevy.Mcp.Configuration;
using Xunit;

namespace Hevy.Mcp.Tests.Configuration;

public sealed class HevyMcpOptionsTests
{
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void FromEnvironmentRejectsMissingOrBlankHevyApiKey(string? apiKey)
  {
    var environment = ValidEnvironment();
    environment["HEVY_API_KEY"] = apiKey;

    var exception = Assert.Throws<InvalidOperationException>(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault));

    Assert.Equal("HEVY_API_KEY is required.", exception.Message);
  }

  [Fact]
  public void FromEnvironmentDefaultsToWritableStdio()
  {
    var options = HevyMcpOptions.FromEnvironment(ValidEnvironment().GetValueOrDefault);

    Assert.Equal(HevyMcpTransport.Stdio, options.Transport);
    Assert.False(options.ReadOnly);
  }

  [Theory]
  [InlineData("stdio", HevyMcpTransport.Stdio)]
  [InlineData("http", HevyMcpTransport.Http)]
  public void FromEnvironmentAcceptsDocumentedTransports(string value, HevyMcpTransport expected)
  {
    var environment = ValidEnvironment();
    environment["HEVY_MCP_TRANSPORT"] = value;
    if (expected is HevyMcpTransport.Http)
    {
      environment["MCP_AUTH_TOKEN"] = "mcp-token";
    }

    var options = HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault);

    Assert.Equal(expected, options.Transport);
  }

  [Theory]
  [InlineData("")]
  [InlineData("STDIO")]
  [InlineData("tcp")]
  [InlineData(" stdio")]
  public void FromEnvironmentRejectsUndocumentedTransports(string value)
  {
    var environment = ValidEnvironment();
    environment["HEVY_MCP_TRANSPORT"] = value;

    var exception = Assert.Throws<InvalidOperationException>(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault));

    Assert.Contains("HEVY_MCP_TRANSPORT", exception.Message, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("true", true)]
  [InlineData("false", false)]
  public void FromEnvironmentAcceptsStrictLowercaseBooleans(string value, bool expected)
  {
    var environment = ValidEnvironment();
    environment["HEVY_READ_ONLY"] = value;

    var options = HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault);

    Assert.Equal(expected, options.ReadOnly);
  }

  [Theory]
  [InlineData("")]
  [InlineData("True")]
  [InlineData("FALSE")]
  [InlineData("1")]
  [InlineData("yes")]
  public void FromEnvironmentRejectsNonStrictBooleans(string value)
  {
    var environment = ValidEnvironment();
    environment["HEVY_READ_ONLY"] = value;

    var exception = Assert.Throws<InvalidOperationException>(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault));

    Assert.Contains("HEVY_READ_ONLY", exception.Message, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void HttpRequiresNonBlankAuthenticationToken(string? token)
  {
    var environment = ValidEnvironment();
    environment["HEVY_MCP_TRANSPORT"] = "http";
    environment["MCP_AUTH_TOKEN"] = token;

    var exception = Assert.Throws<InvalidOperationException>(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault));

    Assert.Equal("MCP_AUTH_TOKEN is required for HTTP transport.", exception.Message);
  }

  [Theory]
  [InlineData(" token")]
  [InlineData("token ")]
  [InlineData("to ken")]
  [InlineData("token?")]
  [InlineData("\"token\"")]
  [InlineData("=token")]
  [InlineData("token=padding")]
  [InlineData("tøken")]
  public void HttpRejectsAuthenticationTokensOutsideBearerToken68Grammar(string token)
  {
    var environment = ValidEnvironment();
    environment["HEVY_MCP_TRANSPORT"] = "http";
    environment["MCP_AUTH_TOKEN"] = token;

    var exception = Assert.Throws<InvalidOperationException>(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault));

    Assert.Equal("MCP_AUTH_TOKEN must use Bearer token68 syntax.", exception.Message);
  }

  [Theory]
  [InlineData("AZaz09-._~+/")]
  [InlineData("token=")]
  [InlineData("token==")]
  [InlineData("token====")]
  public void HttpAcceptsBearerToken68IncludingTrailingPadding(string token)
  {
    var environment = ValidEnvironment();
    environment["HEVY_MCP_TRANSPORT"] = "http";
    environment["MCP_AUTH_TOKEN"] = token;

    var options = HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault);

    Assert.Equal(token, options.McpAuthToken);
  }

  [Fact]
  public void HttpRequiresAuthenticationTokenDistinctFromHevyApiKey()
  {
    var environment = ValidEnvironment();
    environment["HEVY_MCP_TRANSPORT"] = "http";
    environment["MCP_AUTH_TOKEN"] = environment["HEVY_API_KEY"];

    var exception = Assert.Throws<InvalidOperationException>(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault));

    Assert.Contains("distinct", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void ToStringDoesNotRevealSecrets()
  {
    var environment = ValidEnvironment();
    environment["HEVY_MCP_TRANSPORT"] = "http";
    environment["MCP_AUTH_TOKEN"] = "mcp-token-secret";

    var text = HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault).ToString();

    Assert.DoesNotContain("hevy-key-secret", text, StringComparison.Ordinal);
    Assert.DoesNotContain("mcp-token-secret", text, StringComparison.Ordinal);
    Assert.Equal("HevyMcpOptions { Transport = Http, ReadOnly = False }", text);
  }

  private static Dictionary<string, string?> ValidEnvironment() => new(StringComparer.Ordinal)
  {
    ["HEVY_API_KEY"] = "hevy-key-secret",
  };
}
