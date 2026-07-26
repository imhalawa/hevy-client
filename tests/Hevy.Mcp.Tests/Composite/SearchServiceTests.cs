using Hevy.Client.Models;
using Hevy.Mcp.Caching;
using Hevy.Mcp.Composite;
using Microsoft.Extensions.Caching.Memory;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Composite;

public sealed class SearchServiceTests
{
  [Fact]
  public async Task RoutineSearchCollapsesWhitespaceAndUsesInvariantCaseFolding()
  {
    var client = new FakeHevyClient
    {
      Routines = new(1, 1,
      [
        Routine("routine-1", "  Push   Day "),
        Routine("routine-2", "Pull Day"),
      ]),
    };
    using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var service = new SearchService(new HevyCache(client, memory, TimeProvider.System));

    var result = await service.SearchRoutinesAsync("  pUsH\t day ", 100, null, default);

    Assert.Equal("routine-1", Assert.Single(result.Items).Id);
    Assert.Equal("PUSH DAY", result.Filters["query"]);
    Assert.False(result.Truncated);
    Assert.Null(result.Continuation);
  }

  [Fact]
  public async Task ExerciseTemplateSearchAppliesEquipmentAndPrimaryOrSecondaryMuscleFilters()
  {
    var client = new FakeHevyClient
    {
      ExerciseTemplates = new(1, 1,
      [
        Template("template-1", "Incline Press", EquipmentCategory.Barbell, "chest", ["shoulders", "triceps"]),
        Template("template-2", "Shoulder Press", EquipmentCategory.Dumbbell, "shoulders", ["triceps"]),
      ]),
    };
    using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var service = new SearchService(new HevyCache(client, memory, TimeProvider.System));

    var result = await service.SearchExerciseTemplatesAsync(" press ", "BARBELL", " SHOULDERS ", 100, null, default);

    var item = Assert.Single(result.Items);
    Assert.Equal("template-1", item.Id);
    Assert.Equal(EquipmentCategory.Barbell, item.EquipmentCategory);
    Assert.Equal("barbell", result.Filters["equipment"]);
    Assert.Equal("SHOULDERS", result.Filters["muscle"]);
  }

  [Fact]
  public async Task SearchReturnsReusableOpaqueContinuationAndMarksEveryPartialResult()
  {
    var routines = Enumerable.Range(1, 4).Select(index => Routine($"routine-{index}", $"Day {index}")).ToArray();
    var client = new FakeHevyClient { Routines = new(1, 1, routines) };
    using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var service = new SearchService(new HevyCache(client, memory, TimeProvider.System));

    var first = await service.SearchRoutinesAsync("day", 2, null, default);
    var second = await service.SearchRoutinesAsync("day", 2, first.Continuation, default);

    Assert.Equal(["routine-1", "routine-2"], first.Items.Select(static item => item.Id));
    Assert.True(first.Truncated);
    Assert.NotNull(first.Continuation);
    Assert.Equal(["routine-3", "routine-4"], second.Items.Select(static item => item.Id));
    Assert.False(second.Truncated);
    Assert.Null(second.Continuation);
    Assert.Equal(1, client.CallCount);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1001)]
  public async Task SearchRejectsLimitsOutsideOneThroughOneThousand(int limit)
  {
    using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var service = new SearchService(new HevyCache(new FakeHevyClient(), memory, TimeProvider.System));

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchRoutinesAsync("day", limit, null, default));
  }

  [Fact]
  public async Task ContinuationCannotBeReusedWithChangedFilters()
  {
    var client = new FakeHevyClient { Routines = new(1, 1, [Routine("1", "Push"), Routine("2", "Push")]) };
    using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var service = new SearchService(new HevyCache(client, memory, TimeProvider.System));
    var first = await service.SearchRoutinesAsync("push", 1, null, default);

    await Assert.ThrowsAsync<ArgumentException>(() => service.SearchRoutinesAsync("pull", 1, first.Continuation, default));
  }

  private static Routine Routine(string id, string title) => FakeHevyClient.SampleRoutine() with { Id = id, Title = title };

  private static ExerciseTemplate Template(string id, string title, EquipmentCategory equipment, string primary, IReadOnlyList<string> secondary) =>
      new(id, title, "weight_reps", primary, secondary, equipment, false);
}
