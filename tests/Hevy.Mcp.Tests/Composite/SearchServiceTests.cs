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

    ((result.Items).Should().ContainSingle().Which.Id).Should().Be("routine-1");
    (result.Filters["query"]).Should().Be("PUSH DAY");
    (result.Truncated).Should().BeFalse();
    (result.Continuation).Should().BeNull();
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

    var item = (result.Items).Should().ContainSingle().Which;
    (item.Id).Should().Be("template-1");
    (item.EquipmentCategory).Should().Be(EquipmentCategory.Barbell);
    (result.Filters["equipment"]).Should().Be("barbell");
    (result.Filters["muscle"]).Should().Be("SHOULDERS");
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

    (first.Items.Select(static item => item.Id)).Should().Equal(["routine-1", "routine-2"]);
    (first.Truncated).Should().BeTrue();
    (first.Continuation).Should().NotBeNull();
    (second.Items.Select(static item => item.Id)).Should().Equal(["routine-3", "routine-4"]);
    (second.Truncated).Should().BeFalse();
    (second.Continuation).Should().BeNull();
    (client.CallCount).Should().Be(1);
  }

  [Fact]
  public async Task SearchContinuesAcrossCatalogsLargerThanThePerCallScanLimit()
  {
    var client = new FakeHevyClient
    {
      GetRoutinesHandler = (page, pageSize, _) =>
      {
        const int count = 1_010;
        var start = (page - 1) * pageSize + 1;
        var items = Enumerable.Range(start, Math.Min(pageSize, count - start + 1))
            .Select(index => Routine($"routine-{index:D4}", index == count ? "Needle" : $"Day {index:D4}"))
            .ToArray();
        return Task.FromResult(new PagedResult<Routine>(page, 101, items));
      },
    };
    using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var service = new SearchService(new HevyCache(client, memory, TimeProvider.System));

    var first = await service.SearchRoutinesAsync("needle", 1, null, default);
    var second = await service.SearchRoutinesAsync("needle", 1, first.Continuation, default);

    (first.Items).Should().BeEmpty();
    (first.Truncated).Should().BeTrue();
    (first.Continuation).Should().NotBeNull();
    ((second.Items).Should().ContainSingle().Which.Id).Should().Be("routine-1010");
    (second.Truncated).Should().BeFalse();
  }

  [Fact]
  public async Task SearchContinuationDoesNotReplayMatchesAfterAShortNonFinalPage()
  {
    var client = new FakeHevyClient
    {
      GetRoutinesHandler = (page, _, _) => Task.FromResult(page switch
      {
        1 => new PagedResult<Routine>(1, 3, [Routine("skip", "Other")]),
        2 => new PagedResult<Routine>(2, 3, Enumerable.Range(1, 10).Select(index => Routine($"routine-{index}", $"Day {index}")).ToArray()),
        _ => new PagedResult<Routine>(3, 3, [Routine("routine-11", "Day 11")]),
      }),
    };
    using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var service = new SearchService(new HevyCache(client, memory, TimeProvider.System));

    var first = await service.SearchRoutinesAsync("day", 2, null, default);
    var second = await service.SearchRoutinesAsync("day", 2, first.Continuation, default);

    (first.Items.Select(static item => item.Id)).Should().Equal(["routine-1", "routine-2"]);
    (second.Items.Select(static item => item.Id)).Should().Equal(["routine-3", "routine-4"]);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1001)]
  public async Task SearchRejectsLimitsOutsideOneThroughOneThousand(int limit)
  {
    using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var service = new SearchService(new HevyCache(new FakeHevyClient(), memory, TimeProvider.System));

    await FluentActions.Awaiting(() => service.SearchRoutinesAsync("day", limit, null, default)).Should().ThrowExactlyAsync<ArgumentOutOfRangeException>();
  }

  [Fact]
  public async Task ContinuationCannotBeReusedWithChangedFilters()
  {
    var client = new FakeHevyClient { Routines = new(1, 1, [Routine("1", "Push"), Routine("2", "Push")]) };
    using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
    var service = new SearchService(new HevyCache(client, memory, TimeProvider.System));
    var first = await service.SearchRoutinesAsync("push", 1, null, default);

    await FluentActions.Awaiting(() => service.SearchRoutinesAsync("pull", 1, first.Continuation, default)).Should().ThrowExactlyAsync<ArgumentException>();
  }

  private static Routine Routine(string id, string title) => FakeHevyClient.SampleRoutine() with { Id = id, Title = title };

  private static ExerciseTemplate Template(string id, string title, EquipmentCategory equipment, string primary, IReadOnlyList<string> secondary) =>
      new(id, title, "weight_reps", primary, secondary, equipment, false);
}
