using Hevy.Core.Models;

namespace Hevy.Mcp.Tools;

internal sealed record ItemsData<T>(ImmutableList<T> Items) where T : class;
