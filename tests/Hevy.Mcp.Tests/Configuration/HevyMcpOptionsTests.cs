using Hevy.Mcp.Configuration;
using Microsoft.Extensions.Logging;
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

    var exception = FluentActions.Invoking(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault)).Should().ThrowExactly<InvalidOperationException>().Which;

    (exception.Message).Should().Be("HEVY_API_KEY is required.");
  }

  [Fact]
  public void FromEnvironmentDefaultsToWritableStdio()
  {
    var options = HevyMcpOptions.FromEnvironment(ValidEnvironment().GetValueOrDefault);

    (options.ApiKey).Should().Be("hevy-key-secret");
    (options.Transport).Should().Be(HevyMcpTransport.Stdio);
    (options.ReadOnly).Should().BeFalse();
    (options.LogLevel).Should().Be(LogLevel.None);
  }

  [Theory]
  [InlineData("Trace", LogLevel.Trace)]
  [InlineData("Debug", LogLevel.Debug)]
  [InlineData("Information", LogLevel.Information)]
  [InlineData("Warning", LogLevel.Warning)]
  [InlineData("Error", LogLevel.Error)]
  [InlineData("Critical", LogLevel.Critical)]
  [InlineData("None", LogLevel.None)]
  public void FromEnvironmentAcceptsDocumentedLogLevels(string value, LogLevel expected)
  {
    var environment = ValidEnvironment();
    environment["HEVY_LOG_LEVEL"] = value;

    var options = HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault);

    (options.LogLevel).Should().Be(expected);
  }

  [Theory]
  [InlineData("")]
  [InlineData("information")]
  [InlineData("Info")]
  [InlineData("7")]
  public void FromEnvironmentRejectsUndocumentedLogLevels(string value)
  {
    var environment = ValidEnvironment();
    environment["HEVY_LOG_LEVEL"] = value;

    var exception = FluentActions.Invoking(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault)).Should().ThrowExactly<InvalidOperationException>().Which;

    (exception.Message).Should().Contain("HEVY_LOG_LEVEL");
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

    (options.Transport).Should().Be(expected);
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

    var exception = FluentActions.Invoking(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault)).Should().ThrowExactly<InvalidOperationException>().Which;

    (exception.Message).Should().Contain("HEVY_MCP_TRANSPORT");
  }

  [Theory]
  [InlineData("true", true)]
  [InlineData("false", false)]
  public void FromEnvironmentAcceptsStrictLowercaseBooleans(string value, bool expected)
  {
    var environment = ValidEnvironment();
    environment["HEVY_READ_ONLY"] = value;

    var options = HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault);

    (options.ReadOnly).Should().Be(expected);
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

    var exception = FluentActions.Invoking(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault)).Should().ThrowExactly<InvalidOperationException>().Which;

    (exception.Message).Should().Contain("HEVY_READ_ONLY");
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

    var exception = FluentActions.Invoking(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault)).Should().ThrowExactly<InvalidOperationException>().Which;

    (exception.Message).Should().Be("MCP_AUTH_TOKEN is required for HTTP transport.");
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

    var exception = FluentActions.Invoking(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault)).Should().ThrowExactly<InvalidOperationException>().Which;

    (exception.Message).Should().Be("MCP_AUTH_TOKEN must use Bearer token68 syntax.");
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

    (options.McpAuthToken).Should().Be(token);
  }

  [Fact]
  public void HttpRequiresAuthenticationTokenDistinctFromHevyApiKey()
  {
    var environment = ValidEnvironment();
    environment["HEVY_MCP_TRANSPORT"] = "http";
    environment["MCP_AUTH_TOKEN"] = environment["HEVY_API_KEY"];

    var exception = FluentActions.Invoking(() => HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault)).Should().ThrowExactly<InvalidOperationException>().Which;

    (exception.Message).Should().ContainEquivalentOf("distinct");
  }

  [Fact]
  public void ToStringDoesNotRevealSecrets()
  {
    var environment = ValidEnvironment();
    environment["HEVY_MCP_TRANSPORT"] = "http";
    environment["MCP_AUTH_TOKEN"] = "mcp-token-secret";

    var text = HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault).ToString();

    (text).Should().NotContain("hevy-key-secret");
    (text).Should().NotContain("mcp-token-secret");
    (text).Should().Be("HevyMcpOptions { Transport = Http, ReadOnly = False, LogLevel = None }");
  }

  private static Dictionary<string, string?> ValidEnvironment() => new(StringComparer.Ordinal)
  {
    ["HEVY_API_KEY"] = "hevy-key-secret",
  };
}
