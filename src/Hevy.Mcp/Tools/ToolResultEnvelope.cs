namespace Hevy.Mcp.Tools;

internal sealed record ToolResultEnvelope(bool Ok, object? Data = null, ToolError? Error = null, object? Meta = null);
