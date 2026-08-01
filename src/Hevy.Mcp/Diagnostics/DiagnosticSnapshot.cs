using System.Runtime.InteropServices;
using Hevy.Mcp.Configuration;

namespace Hevy.Mcp.Diagnostics;

internal sealed record DiagnosticSnapshot(
    string ServerVersion,
    string RuntimeVersion,
    string Transport,
    bool ReadOnly,
    bool DiagnosticsEnabled,
    string Health)
{
  internal static DiagnosticSnapshot Create(HevyMcpOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    return new DiagnosticSnapshot(
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        RuntimeInformation.FrameworkDescription,
        options.Transport is HevyMcpTransport.Stdio ? "stdio" : "http",
        options.ReadOnly,
        options.LogLevel is not LogLevel.None,
        "ready");
  }
}
