using Hevy.Core.Models;

namespace Hevy.Mcp.Tools;

internal sealed record ToolOutput<TData, TMeta>(
    bool Ok,
    TData? Data = default,
    ToolError? Error = null,
    TMeta? Meta = default)
    where TData : class
    where TMeta : class;
