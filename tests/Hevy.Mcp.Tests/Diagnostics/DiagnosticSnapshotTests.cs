using System.Text.Json;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Diagnostics;
using Hevy.Mcp.Tools;
using Xunit;

namespace Hevy.Mcp.Tests.Diagnostics;

public sealed class DiagnosticSnapshotTests
{
  [Theory]
  [InlineData("stdio", false)]
  [InlineData("http", true)]
  public void SnapshotContainsOnlyAllowlistedRuntimeState(string transport, bool readOnly)
  {
    var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["HEVY_API_KEY"] = "fixture-api-key-never-output",
      ["HEVY_MCP_TRANSPORT"] = transport,
      ["HEVY_READ_ONLY"] = readOnly ? "true" : "false",
      ["HEVY_LOG_LEVEL"] = "Warning",
      ["MCP_AUTH_TOKEN"] = transport == "http" ? "fixture-mcp-token-never-output" : null,
    };
    var options = HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault);

    var snapshot = DiagnosticSnapshot.Create(options);
    var json = JsonSerializer.Serialize(snapshot, ToolResults.JsonOptions);

    (string.IsNullOrWhiteSpace(snapshot.ServerVersion)).Should().BeFalse();
    (string.IsNullOrWhiteSpace(snapshot.RuntimeVersion)).Should().BeFalse();
    (snapshot.Transport).Should().Be(transport);
    (snapshot.ReadOnly).Should().Be(readOnly);
    (snapshot.DiagnosticsEnabled).Should().BeTrue();
    (snapshot.Health).Should().Be("ready");
    (json).Should().NotContain("fixture-api-key-never-output");
    (json).Should().NotContain("fixture-mcp-token-never-output");
    (json).Should().NotContainEquivalentOf("header");
    (json).Should().NotContainEquivalentOf("query");
    (json).Should().NotContainEquivalentOf("measurement");
  }
}
