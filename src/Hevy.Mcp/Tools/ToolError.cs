using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using Hevy.Client;
using Hevy.Core.Models;
using Hevy.Mcp.Caching;

namespace Hevy.Mcp.Tools;

internal sealed record ToolError(
    string Code,
    string Message,
    bool Retryable,
    string CorrelationId,
    int? HevyStatus = null,
    string? HevyRequestId = null);
