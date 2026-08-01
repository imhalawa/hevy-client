using System.Text.Json;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Hevy.Mcp.Diagnostics;

internal enum DiagnosticExceptionCategory
{
  None,
  Validation,
  Upstream,
  OutcomeUnknown,
  Unexpected,
  Cancellation,
}
