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

    Assert.False(string.IsNullOrWhiteSpace(snapshot.ServerVersion));
    Assert.False(string.IsNullOrWhiteSpace(snapshot.RuntimeVersion));
    Assert.Equal(transport, snapshot.Transport);
    Assert.Equal(readOnly, snapshot.ReadOnly);
    Assert.True(snapshot.DiagnosticsEnabled);
    Assert.Equal("ready", snapshot.Health);
    Assert.DoesNotContain("fixture-api-key-never-output", json, StringComparison.Ordinal);
    Assert.DoesNotContain("fixture-mcp-token-never-output", json, StringComparison.Ordinal);
    Assert.DoesNotContain("header", json, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("query", json, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("measurement", json, StringComparison.OrdinalIgnoreCase);
  }
}
