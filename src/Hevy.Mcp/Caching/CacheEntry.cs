namespace Hevy.Mcp.Caching;

internal sealed class CacheEntry<T>(
    Lazy<Task<ImmutableList<T>>> load,
    CancellationTokenSource fillCancellation,
    DateTimeOffset lastAccess)
    where T : class
{
  internal Lazy<Task<ImmutableList<T>>> Load { get; } = load;
  internal CancellationTokenSource FillCancellation { get; } = fillCancellation;
  internal DateTimeOffset LastAccess { get; set; } = lastAccess;
  internal int WaiterCount { get; set; }
}
