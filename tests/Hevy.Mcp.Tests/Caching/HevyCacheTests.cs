using Hevy.Client.Models;
using Hevy.Mcp.Caching;
using Microsoft.Extensions.Caching.Memory;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Caching;

public sealed class HevyCacheTests
{
  [Fact]
  public async Task ConcurrentRoutineCatalogRequestsShareOneCompleteLoad()
  {
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var client = new FakeHevyClient
    {
      GetRoutinesHandler = async (page, _, cancellationToken) =>
      {
        await release.Task.WaitAsync(cancellationToken);
        return new PagedResult<Routine>(page, 1, [FakeHevyClient.SampleRoutine()]);
      },
    };
    using var memory = CreateMemoryCache(100);
    var cache = new HevyCache(client, memory, TimeProvider.System);

    var first = cache.GetRoutinesAsync(default);
    var second = cache.GetRoutinesAsync(default);
    release.SetResult();

    Assert.Same(await first, await second);
    Assert.Equal(1, client.CallCount);
  }

  [Fact]
  public async Task TemplateCatalogUsesSlidingFifteenMinuteExpiryUnderInjectedTime()
  {
    var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));
    var client = new FakeHevyClient
    {
      ExerciseTemplates = new(1, 1, [Template("template-1", "Squat")]),
    };
    using var memory = CreateMemoryCache(100);
    var cache = new HevyCache(client, memory, clock);

    await cache.GetExerciseTemplatesAsync(default);
    clock.Advance(TimeSpan.FromMinutes(14));
    await cache.GetExerciseTemplatesAsync(default);
    clock.Advance(TimeSpan.FromMinutes(14));
    await cache.GetExerciseTemplatesAsync(default);
    Assert.Equal(1, client.CallCount);

    clock.Advance(TimeSpan.FromMinutes(15));
    await cache.GetExerciseTemplatesAsync(default);
    Assert.Equal(2, client.CallCount);
  }

  [Fact]
  public async Task BoundedMemoryEvictsCatalogAndCacheKeysContainNoCredentialMaterial()
  {
    var client = new FakeHevyClient
    {
      Routines = new(1, 1, [FakeHevyClient.SampleRoutine()]),
      ExerciseTemplates = new(1, 1, [Template("template-1", "Squat")]),
    };
    using var memory = CreateMemoryCache(1);
    var cache = new HevyCache(client, memory, TimeProvider.System);

    await cache.GetRoutinesAsync(default);
    await cache.GetExerciseTemplatesAsync(default);
    await cache.GetExerciseTemplatesAsync(default);

    Assert.Equal(3, client.CallCount);
    Assert.DoesNotContain("key", string.Join(',', HevyCache.CacheKeyNames), StringComparison.OrdinalIgnoreCase);
    Assert.Equal(["exercise-templates", "routines"], HevyCache.CacheKeyNames.Order());
  }

  [Fact]
  public async Task RelatedInvalidationReloadsOnlyTheAffectedCatalog()
  {
    var client = new FakeHevyClient
    {
      Routines = new(1, 1, [FakeHevyClient.SampleRoutine()]),
      ExerciseTemplates = new(1, 1, [Template("template-1", "Squat")]),
    };
    using var memory = CreateMemoryCache(100);
    var cache = new HevyCache(client, memory, TimeProvider.System);
    await cache.GetRoutinesAsync(default);
    await cache.GetExerciseTemplatesAsync(default);

    cache.InvalidateRoutines();
    await cache.GetRoutinesAsync(default);
    await cache.GetExerciseTemplatesAsync(default);
    cache.InvalidateExerciseTemplates();
    await cache.GetExerciseTemplatesAsync(default);

    Assert.Equal(4, client.CallCount);
  }

  [Fact]
  public async Task FailedOrInconsistentCatalogLoadsAreNeverCached()
  {
    var attempts = 0;
    var client = new FakeHevyClient
    {
      GetRoutinesHandler = (page, _, _) =>
      {
        attempts++;
        return attempts == 1
            ? Task.FromException<PagedResult<Routine>>(new InvalidOperationException("upstream failed"))
            : Task.FromResult(new PagedResult<Routine>(page, page == 1 ? 2 : 3, [FakeHevyClient.SampleRoutine()]));
      },
    };
    using var memory = CreateMemoryCache(100);
    var cache = new HevyCache(client, memory, TimeProvider.System);

    await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetRoutinesAsync(default));
    await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetRoutinesAsync(default));
    await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetRoutinesAsync(default));

    Assert.True(client.CallCount >= 3);
  }

  [Fact]
  public async Task CreatorCancellationCancelsOnlyItsWaitAndSharedLoadCompletesForAnotherWaiter()
  {
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var client = new FakeHevyClient
    {
      GetRoutinesHandler = async (page, _, _) =>
      {
        started.SetResult();
        await release.Task;
        return new PagedResult<Routine>(page, 1, [FakeHevyClient.SampleRoutine()]);
      },
    };
    using var memory = CreateMemoryCache(100);
    var cache = new HevyCache(client, memory, TimeProvider.System);
    using var creatorCancellation = new CancellationTokenSource();

    var creator = cache.GetRoutinesAsync(creatorCancellation.Token);
    await started.Task;
    var survivor = cache.GetRoutinesAsync(default);
    creatorCancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => creator);
    release.SetResult();

    Assert.Single(await survivor);
    Assert.Equal(1, client.CallCount);
    Assert.Single(await cache.GetRoutinesAsync(default));
    Assert.Equal(1, client.CallCount);
  }

  [Fact]
  public async Task LaterWaiterCancellationDoesNotCancelCreatorOrEvictSharedLoad()
  {
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var client = new FakeHevyClient
    {
      GetExerciseTemplatesHandler = async (page, _, _) =>
      {
        started.SetResult();
        await release.Task;
        return new PagedResult<ExerciseTemplate>(page, 1, [Template("template-1", "Squat")]);
      },
    };
    using var memory = CreateMemoryCache(100);
    var cache = new HevyCache(client, memory, TimeProvider.System);
    var creator = cache.GetExerciseTemplatesAsync(default);
    await started.Task;
    using var waiterCancellation = new CancellationTokenSource();
    var cancelledWaiter = cache.GetExerciseTemplatesAsync(waiterCancellation.Token);
    waiterCancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWaiter);
    release.SetResult();

    Assert.Single(await creator);
    Assert.Equal(1, client.CallCount);
  }

  [Fact]
  public async Task SoleWaiterCancellationCancelsUnderlyingFillAndNextCallerStartsFresh()
  {
    var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var fillCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var attempts = 0;
    var client = new FakeHevyClient
    {
      GetRoutinesHandler = async (page, _, cancellationToken) =>
      {
        attempts++;
        if (attempts == 1)
        {
          firstStarted.SetResult();
          try
          {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
          }
          catch (OperationCanceledException)
          {
            fillCancelled.SetResult();
            throw;
          }
        }
        return new PagedResult<Routine>(page, 1, [FakeHevyClient.SampleRoutine()]);
      },
    };
    using var memory = CreateMemoryCache(100);
    var cache = new HevyCache(client, memory, TimeProvider.System);
    using var cancellation = new CancellationTokenSource();
    var abandoned = cache.GetRoutinesAsync(cancellation.Token);
    await firstStarted.Task;

    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
    await fillCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    Assert.Single(await cache.GetRoutinesAsync(default));
    Assert.Equal(2, client.CallCount);
  }

  [Fact]
  public async Task CancellingAllWaitersCancelsTheirOneUnderlyingFill()
  {
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var fillCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var client = new FakeHevyClient
    {
      GetExerciseTemplatesHandler = async (_, _, cancellationToken) =>
      {
        started.SetResult();
        try
        {
          await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
          fillCancelled.SetResult();
          throw;
        }
        return new PagedResult<ExerciseTemplate>(1, 0, []);
      },
    };
    using var memory = CreateMemoryCache(100);
    var cache = new HevyCache(client, memory, TimeProvider.System);
    using var firstCancellation = new CancellationTokenSource();
    using var secondCancellation = new CancellationTokenSource();
    var first = cache.GetExerciseTemplatesAsync(firstCancellation.Token);
    var second = cache.GetExerciseTemplatesAsync(secondCancellation.Token);
    await started.Task;

    firstCancellation.Cancel();
    secondCancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
    await fillCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    Assert.Equal(1, client.CallCount);
  }

  private static MemoryCache CreateMemoryCache(long sizeLimit) => new(new MemoryCacheOptions { SizeLimit = sizeLimit });

  private static ExerciseTemplate Template(string id, string title) =>
      new(id, title, "weight_reps", "quadriceps", ["glutes"], EquipmentCategory.Barbell, false);

  private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
  {
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    internal void Advance(TimeSpan duration) => _now += duration;
  }
}
