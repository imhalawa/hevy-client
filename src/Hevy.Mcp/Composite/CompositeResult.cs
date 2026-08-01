using System.Globalization;
using Hevy.Core.Models;
using Hevy.Mcp.Caching;

namespace Hevy.Mcp.Composite;

internal sealed record CompositeResult<T>(
    ImmutableList<T> Items,
    IReadOnlyDictionary<string, string?> Filters,
    int Limit,
    bool Truncated,
    string? Continuation)
    where T : class;
