using Hevy.Client;
using Hevy.Core.Models;
using Hevy.Mcp.Caching;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class ReadToolTests
{
  [Fact]
  public async Task LowLevelRoutineAndTemplateReadsShareCompleteCatalogCacheWithExactLocalPaging()
  {
    var routines = Enumerable.Range(1, 12).Select(index => FakeHevyClient.SampleRoutine() with { Id = $"routine-{index:D2}", Title = $"Routine {index:D2}" }).ToArray();
    var templates = Enumerable.Range(1, 12).Select(index => new ExerciseTemplate($"template-{index:D2}", $"Template {index:D2}", "weight_reps", "quadriceps", [], EquipmentCategory.Barbell, false)).ToArray();
    var client = new FakeHevyClient
    {
      GetRoutinesHandler = (page, pageSize, _) => Task.FromResult(new PagedResult<Routine>(page, 2, routines.Skip((page - 1) * pageSize).Take(pageSize).ToImmutableList())),
      GetExerciseTemplatesHandler = (page, pageSize, _) => Task.FromResult(new PagedResult<ExerciseTemplate>(page, 2, templates.Skip((page - 1) * pageSize).Take(pageSize).ToImmutableList())),
    };
    using var services = CachedServices(client);

    var routinePage = await RoutineReadTools.GetRoutines(services, 2, 5, "compact", default);
    var routine = await RoutineReadTools.GetRoutine(services, "routine-11", default);
    var templatePage = await ExerciseReadTools.GetExerciseTemplates(services, 2, 5, "full", default);
    var template = await ExerciseReadTools.GetExerciseTemplate(services, "template-11", default);
    await RoutineReadTools.GetRoutines(services, 1, 10, "full", default);
    await ExerciseReadTools.GetExerciseTemplates(services, 1, 10, "compact", default);

    (routinePage.Structured().GetProperty("data").GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString())).Should().Equal(["routine-06", "routine-07", "routine-08", "routine-09", "routine-10"]);
    (routinePage.Structured().GetProperty("meta").GetProperty("page_count").GetInt32()).Should().Be(3);
    (routine.Structured().GetProperty("data").GetProperty("id").GetString()).Should().Be("routine-11");
    (templatePage.Structured().GetProperty("data").GetProperty("items").GetArrayLength()).Should().Be(5);
    (template.Structured().GetProperty("data").GetProperty("id").GetString()).Should().Be("template-11");
    (client.CallCount).Should().Be(4);
  }

  [Fact]
  public async Task ExerciseTemplatePagingAcceptsOneHundredButRejectsOneHundredAndOneBeforeIo()
  {
    var acceptedPageSize = 0;
    var client = new FakeHevyClient
    {
      GetExerciseTemplatesHandler = (page, pageSize, _) =>
      {
        acceptedPageSize = pageSize;
        return Task.FromResult(new PagedResult<ExerciseTemplate>(page, 1, []));
      },
    };
    var services = Services(client);

    var accepted = await ExerciseReadTools.GetExerciseTemplates(services, 1, 100, "compact", default);
    var rejected = await ExerciseReadTools.GetExerciseTemplates(services, 1, 101, "compact", default);

    (accepted.IsError).Should().BeFalse();
    (acceptedPageSize).Should().Be(100);
    (rejected.IsError).Should().BeTrue();
    (rejected.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (client.CallCount).Should().Be(1);
  }

  [Fact]
  public async Task ExerciseTemplatePresentationPageSizeDoesNotIncreaseInternalCatalogFetches()
  {
    var upstreamPageSizes = new List<int>();
    var templates = Enumerable.Range(1, 12)
        .Select(index => new ExerciseTemplate($"template-{index:D2}", $"Template {index:D2}", "weight_reps", "quadriceps", [], EquipmentCategory.Barbell, false))
        .ToArray();
    var client = new FakeHevyClient
    {
      GetExerciseTemplatesHandler = (page, pageSize, _) =>
      {
        upstreamPageSizes.Add(pageSize);
        return Task.FromResult(new PagedResult<ExerciseTemplate>(page, 2, templates.Skip((page - 1) * pageSize).Take(pageSize).ToImmutableList()));
      },
    };
    using var services = CachedServices(client);

    var result = await ExerciseReadTools.GetExerciseTemplates(services, 1, 100, "compact", default);

    (result.IsError).Should().BeFalse();
    (result.Structured().GetProperty("data").GetProperty("items").GetArrayLength()).Should().Be(12);
    (upstreamPageSizes).Should().Equal([10, 10]);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Cached_catalog_paging_rejects_pages_beyond_the_computed_page_count(bool templates)
  {
    var client = new FakeHevyClient
    {
      Routines = new(1, 1, [FakeHevyClient.SampleRoutine()]),
      ExerciseTemplates = new(1, 1, [new ExerciseTemplate("template-1", "Squat", "weight_reps", "quadriceps", [], EquipmentCategory.Barbell, false)]),
    };
    using var services = CachedServices(client);

    var result = templates
        ? await ExerciseReadTools.GetExerciseTemplates(services, 2, 10, "compact", default)
        : await RoutineReadTools.GetRoutines(services, 2, 10, "compact", default);

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
  }

  [Fact]
  public async Task LowLevelCatalogReadsExpireAndReloadAfterSuccessfulRelatedMutations()
  {
    var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-26T12:00:00Z"));
    var client = new FakeHevyClient
    {
      Routines = new(1, 1, [FakeHevyClient.SampleRoutine()]),
      ExerciseTemplates = new(1, 1, [new ExerciseTemplate("template-1", "Squat", "weight_reps", "quadriceps", [], EquipmentCategory.Barbell, false)]),
    };
    using var services = CachedServices(client, clock);

    await RoutineReadTools.GetRoutines(services, 1, 10, "compact", default);
    await RoutineReadTools.GetRoutine(services, "routine-1", default);
    await ExerciseReadTools.GetExerciseTemplates(services, 1, 10, "compact", default);
    await ExerciseReadTools.GetExerciseTemplate(services, "template-1", default);
    (client.CallCount).Should().Be(2);

    clock.Advance(TimeSpan.FromMinutes(15));
    await RoutineReadTools.GetRoutines(services, 1, 10, "compact", default);
    await ExerciseReadTools.GetExerciseTemplates(services, 1, 10, "compact", default);
    (client.CallCount).Should().Be(4);

    await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineCommand(), false, default);
    await RoutineReadTools.GetRoutine(services, "routine-1", default);
    await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateCommand(), false, default);
    await ExerciseReadTools.GetExerciseTemplate(services, "template-1", default);
    (client.CallCount).Should().Be(8);
  }
  [Fact]
  public async Task EveryReadHandlerInvokesItsMatchingClientOperation()
  {
    var client = new FakeHevyClient();
    var services = Services(client);
    var since = DateTimeOffset.Parse("2026-07-01T00:00:00Z");

    await WorkoutReadTools.GetWorkoutCount(services, CancellationToken.None);
    await WorkoutReadTools.GetWorkoutEvents(services, 1, 10, since, "compact", CancellationToken.None);
    await WorkoutReadTools.GetWorkout(services, "workout-1", CancellationToken.None);
    await RoutineReadTools.GetRoutines(services, 1, 10, "compact", CancellationToken.None);
    await RoutineReadTools.GetRoutine(services, "routine-1", CancellationToken.None);
    await RoutineReadTools.GetRoutineFolders(services, 1, 10, "compact", CancellationToken.None);
    await RoutineReadTools.GetRoutineFolder(services, 1, CancellationToken.None);
    await ExerciseReadTools.GetExerciseTemplates(services, 1, 10, "compact", CancellationToken.None);
    await ExerciseReadTools.GetExerciseTemplate(services, "template-1", CancellationToken.None);
    await ExerciseReadTools.GetExerciseHistory(services, "template-1", 1, 10, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 25), "compact", CancellationToken.None);
    await MeasurementReadTools.GetBodyMeasurements(services, 1, 10, "compact", CancellationToken.None);
    await MeasurementReadTools.GetBodyMeasurement(services, new DateOnly(2026, 7, 25), CancellationToken.None);
    await UserTools.GetUserInfo(services, CancellationToken.None);

    (client.CallCount).Should().Be(13);
  }

  [Fact]
  public async Task ExerciseHistoryRejectsAnInvertedDateRangeBeforeClientIo()
  {
    var client = new FakeHevyClient();

    var result = await ExerciseReadTools.GetExerciseHistory(
        Services(client), "template-1", 1, 10, new DateOnly(2026, 7, 25), new DateOnly(2026, 7, 1), "full", CancellationToken.None);

    (result.IsError).Should().BeTrue();
    (client.CallCount).Should().Be(0);
  }

  [Fact]
  public async Task ExerciseHistoryRejectsAPageBeyondTheStreamingScanBudgetBeforeClientIo()
  {
    var client = new FakeHevyClient();

    var result = await ExerciseReadTools.GetExerciseHistory(
        Services(client), "template-1", 101, 10, null, null, "full", CancellationToken.None);

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (client.CallCount).Should().Be(0);
  }

  [Fact]
  public async Task UpstreamErrorsUseTheStableSafeEnvelope()
  {
    var client = new FakeHevyClient
    {
      GetWorkoutHandler = (_, _) => throw new Hevy.Core.Exceptions.HevyException("not_found", "The workout was not found.", false, System.Net.HttpStatusCode.NotFound),
    };

    var result = await WorkoutReadTools.GetWorkout(Services(client), "missing", CancellationToken.None);
    var error = result.Structured().GetProperty("error");

    (result.IsError).Should().BeTrue();
    (error.GetProperty("code").GetString()).Should().Be("not_found");
    (error.GetProperty("hevy_status").GetInt32()).Should().Be(404);
    (error.GetProperty("correlation_id").GetString()!.Length).Should().Be(32);
    (result.Content[0].ToString()).Should().NotContain("System.");
  }

  [Fact]
  public async Task WorkoutEventContinuationPreservesSinceAndDetailInputs()
  {
    var client = new FakeHevyClient { WorkoutEvents = new PagedResult<WorkoutEvent>(2, 3, []) };
    var since = DateTimeOffset.Parse("2026-07-01T01:02:03Z");

    var result = await WorkoutReadTools.GetWorkoutEvents(Services(client), 2, 4, since, "full", CancellationToken.None);
    var next = result.Structured().GetProperty("meta").GetProperty("continuation");

    (next.GetProperty("page").GetInt32()).Should().Be(3);
    (next.GetProperty("page_size").GetInt32()).Should().Be(4);
    (next.GetProperty("detail").GetString()).Should().Be("full");
    (next.GetProperty("since").GetString()).Should().Be("2026-07-01T01:02:03+00:00");
  }

  [Fact]
  public async Task ExerciseHistoryContinuationPreservesIdentityAndDateFilters()
  {
    var client = new FakeHevyClient
    {
      GetExerciseHistoryWindowHandler = (_, request, _) => Task.FromResult(new ExerciseHistoryWindow([], true, request.Offset + request.Limit)),
    };

    var result = await ExerciseReadTools.GetExerciseHistory(
        Services(client), "template-1", 1, 7, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 25), "compact", CancellationToken.None);
    var next = result.Structured().GetProperty("meta").GetProperty("continuation");

    (next.GetProperty("exercise_template_id").GetString()).Should().Be("template-1");
    (next.GetProperty("page").GetInt32()).Should().Be(2);
    (next.GetProperty("page_size").GetInt32()).Should().Be(7);
    (next.GetProperty("start_date").GetString()).Should().Be("2026-07-01");
    (next.GetProperty("end_date").GetString()).Should().Be("2026-07-25");
    (next.GetProperty("detail").GetString()).Should().Be("compact");
    (result.Structured().GetProperty("meta").GetProperty("scanned_item_count").GetInt32()).Should().BeInRange(1, 1_000);
  }

  private static IServiceProvider Services(IHevyClient client) => new ServiceCollection()
      .AddSingleton(client)
      .BuildServiceProvider();

  private static ServiceProvider CachedServices(IHevyClient client, TimeProvider? timeProvider = null) => new ServiceCollection()
      .AddSingleton(client)
      .AddMemoryCache(memory => memory.SizeLimit = 2)
      .AddSingleton(timeProvider ?? TimeProvider.System)
      .AddSingleton<HevyCache>()
      .BuildServiceProvider();

  private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
  {
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    internal void Advance(TimeSpan duration) => _now += duration;
  }
}
