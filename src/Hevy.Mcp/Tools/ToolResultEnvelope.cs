using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using Hevy.Client;
using Hevy.Core.Models;
using Hevy.Mcp.Caching;

namespace Hevy.Mcp.Tools;

internal sealed record ToolResultEnvelope(bool Ok, object? Data = null, ToolError? Error = null, object? Meta = null);
