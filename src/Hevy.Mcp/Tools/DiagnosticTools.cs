using System.ComponentModel;
using Hevy.Mcp.Diagnostics;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class DiagnosticTools
{
  [McpServerTool(Name = "get_diagnostics", ReadOnly = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<DiagnosticSnapshot, NoMeta>))]
  [Description("Get allowlist-only server version, runtime, transport, read-only mode, diagnostics state, and health. Returns no account, request, or fitness data.")]
  internal static CallToolResult GetDiagnostics(IServiceProvider services)
  {
    var snapshot = ToolResults.Service<DiagnosticSnapshot>(services);
    return ToolResults.Success(snapshot, "Returned privacy-safe server diagnostics.");
  }
}
