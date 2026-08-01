using System.Text.Json;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Hevy.Mcp.Diagnostics;

internal enum DiagnosticOperationStatus
{
  Succeeded,
  Rejected,
  Failed,
  Cancelled,
}
