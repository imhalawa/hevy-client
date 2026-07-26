using Hevy.Client;
using Hevy.Client.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Hevy.Mcp.Caching;

internal sealed class HevyCache
{
  private const string RoutinesKey = "routines";
  private const string ExerciseTemplatesKey = "exercise-templates";
  private const int PageSize = 10;
  private const int MaximumCatalogItems = 1_000;
  private static readonly TimeSpan SlidingLifetime = TimeSpan.FromMinutes(15);
  private readonly IHevyClient _client;
  private readonly IMemoryCache _memory;
  private readonly TimeProvider _timeProvider;
  private readonly object _sync = new();

  public HevyCache(IHevyClient client, IMemoryCache memory, TimeProvider timeProvider)
  {
    _client = client ?? throw new ArgumentNullException(nameof(client));
    _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
  }

  internal static IReadOnlyList<string> CacheKeyNames { get; } = [RoutinesKey, ExerciseTemplatesKey];

  internal Task<IReadOnlyList<Routine>> GetRoutinesAsync(CancellationToken cancellationToken) =>
      GetCatalogAsync(RoutinesKey, _client.GetRoutinesAsync, cancellationToken);

  internal Task<IReadOnlyList<ExerciseTemplate>> GetExerciseTemplatesAsync(CancellationToken cancellationToken) =>
      GetCatalogAsync(ExerciseTemplatesKey, _client.GetExerciseTemplatesAsync, cancellationToken);

  internal void InvalidateRoutines() => _memory.Remove(RoutinesKey);

  internal void InvalidateExerciseTemplates() => _memory.Remove(ExerciseTemplatesKey);

  private async Task<IReadOnlyList<T>> GetCatalogAsync<T>(
      string key,
      Func<int, int, CancellationToken, Task<PagedResult<T>>> readPage,
      CancellationToken cancellationToken)
      where T : class
  {
    CacheEntry<T> entry;
    lock (_sync)
    {
      var now = _timeProvider.GetUtcNow();
      if (_memory.TryGetValue(key, out CacheEntry<T>? cached) && cached is not null)
      {
        if (now - cached.LastAccess < SlidingLifetime)
        {
          cached.LastAccess = now;
          entry = cached;
        }
        else
        {
          _memory.Remove(key);
          entry = CreateEntry(key, readPage, now, cancellationToken);
        }
      }
      else
      {
        entry = CreateEntry(key, readPage, now, cancellationToken);
      }
    }

    try
    {
      return await entry.Load.Value.ConfigureAwait(false);
    }
    catch
    {
      lock (_sync)
      {
        if (_memory.TryGetValue(key, out CacheEntry<T>? current) && ReferenceEquals(current, entry))
        {
          _memory.Remove(key);
        }
      }
      throw;
    }
  }

  private CacheEntry<T> CreateEntry<T>(
      string key,
      Func<int, int, CancellationToken, Task<PagedResult<T>>> readPage,
      DateTimeOffset now,
      CancellationToken cancellationToken)
      where T : class
  {
    var entry = new CacheEntry<T>(
        new Lazy<Task<IReadOnlyList<T>>>(() => LoadCompleteCatalogAsync(readPage, cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication),
        now);
    _memory.Set(key, entry, new MemoryCacheEntryOptions { Size = 1 });
    return entry;
  }

  private static async Task<IReadOnlyList<T>> LoadCompleteCatalogAsync<T>(
      Func<int, int, CancellationToken, Task<PagedResult<T>>> readPage,
      CancellationToken cancellationToken)
      where T : class
  {
    var items = new List<T>();
    var expectedPageCount = -1;
    for (var page = 1; ; page++)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var result = await readPage(page, PageSize, cancellationToken).ConfigureAwait(false);
      if (result.Page != page || result.PageCount < 0 || (expectedPageCount >= 0 && result.PageCount != expectedPageCount))
      {
        throw new InvalidOperationException("Hevy returned inconsistent catalog pagination; the partial catalog was not cached.");
      }

      expectedPageCount = result.PageCount;
      if (expectedPageCount * PageSize > MaximumCatalogItems)
      {
        throw new InvalidOperationException($"The catalog exceeds the bounded {MaximumCatalogItems}-item cache limit.");
      }

      items.AddRange(result.Items);
      if (items.Count > MaximumCatalogItems)
      {
        throw new InvalidOperationException($"The catalog exceeds the bounded {MaximumCatalogItems}-item cache limit.");
      }

      if (page >= expectedPageCount)
      {
        return items.AsReadOnly();
      }
    }
  }

  private sealed class CacheEntry<T>(Lazy<Task<IReadOnlyList<T>>> load, DateTimeOffset lastAccess)
      where T : class
  {
    internal Lazy<Task<IReadOnlyList<T>>> Load { get; } = load;
    internal DateTimeOffset LastAccess { get; set; } = lastAccess;
  }
}
