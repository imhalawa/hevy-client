using Hevy.Client;
using Hevy.Client.Models;
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
      GetRoutinesHandler = (page, pageSize, _) => Task.FromResult(new PagedResult<Routine>(page, 2, routines.Skip((page - 1) * pageSize).Take(pageSize).ToArray())),
      GetExerciseTemplatesHandler = (page, pageSize, _) => Task.FromResult(new PagedResult<ExerciseTemplate>(page, 2, templates.Skip((page - 1) * pageSize).Take(pageSize).ToArray())),
    };
    using var services = CachedServices(client);

    var routinePage = await RoutineReadTools.GetRoutines(services, 2, 5, "compact", default);
    var routine = await RoutineReadTools.GetRoutine(services, "routine-11", default);
    var templatePage = await ExerciseReadTools.GetExerciseTemplates(services, 2, 5, "full", default);
    var template = await ExerciseReadTools.GetExerciseTemplate(services, "template-11", default);
    await RoutineReadTools.GetRoutines(services, 1, 10, "full", default);
    await ExerciseReadTools.GetExerciseTemplates(services, 1, 10, "compact", default);

    Assert.Equal(["routine-06", "routine-07", "routine-08", "routine-09", "routine-10"], routinePage.Structured().GetProperty("data").GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
    Assert.Equal(3, routinePage.Structured().GetProperty("meta").GetProperty("page_count").GetInt32());
    Assert.Equal("routine-11", routine.Structured().GetProperty("data").GetProperty("id").GetString());
    Assert.Equal(5, templatePage.Structured().GetProperty("data").GetProperty("items").GetArrayLength());
    Assert.Equal("template-11", template.Structured().GetProperty("data").GetProperty("id").GetString());
    Assert.Equal(4, client.CallCount);
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
    Assert.Equal(2, client.CallCount);

    clock.Advance(TimeSpan.FromMinutes(15));
    await RoutineReadTools.GetRoutines(services, 1, 10, "compact", default);
    await ExerciseReadTools.GetExerciseTemplates(services, 1, 10, "compact", default);
    Assert.Equal(4, client.CallCount);

    await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineRequest(), false, default);
    await RoutineReadTools.GetRoutine(services, "routine-1", default);
    await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateRequest(), false, default);
    await ExerciseReadTools.GetExerciseTemplate(services, "template-1", default);
    Assert.Equal(8, client.CallCount);
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

    Assert.Equal(13, client.CallCount);
  }

  [Fact]
  public async Task ExerciseHistoryRejectsAnInvertedDateRangeBeforeClientIo()
  {
    var client = new FakeHevyClient();

    var result = await ExerciseReadTools.GetExerciseHistory(
        Services(client), "template-1", 1, 10, new DateOnly(2026, 7, 25), new DateOnly(2026, 7, 1), "full", CancellationToken.None);

    Assert.True(result.IsError);
    Assert.Equal(0, client.CallCount);
  }

  [Fact]
  public async Task UpstreamErrorsUseTheStableSafeEnvelope()
  {
    var client = new FakeHevyClient
    {
      GetWorkoutHandler = (_, _) => throw new Hevy.Client.Errors.HevyException("not_found", "The workout was not found.", false, System.Net.HttpStatusCode.NotFound),
    };

    var result = await WorkoutReadTools.GetWorkout(Services(client), "missing", CancellationToken.None);
    var error = result.Structured().GetProperty("error");

    Assert.True(result.IsError);
    Assert.Equal("not_found", error.GetProperty("code").GetString());
    Assert.Equal(404, error.GetProperty("hevy_status").GetInt32());
    Assert.Equal(32, error.GetProperty("correlation_id").GetString()!.Length);
    Assert.DoesNotContain("System.", result.Content[0].ToString(), StringComparison.Ordinal);
  }

  [Fact]
  public async Task WorkoutEventContinuationPreservesSinceAndDetailInputs()
  {
    var client = new FakeHevyClient { WorkoutEvents = new PagedResult<WorkoutEvent>(2, 3, []) };
    var since = DateTimeOffset.Parse("2026-07-01T01:02:03Z");

    var result = await WorkoutReadTools.GetWorkoutEvents(Services(client), 2, 4, since, "full", CancellationToken.None);
    var next = result.Structured().GetProperty("meta").GetProperty("continuation");

    Assert.Equal(3, next.GetProperty("page").GetInt32());
    Assert.Equal(4, next.GetProperty("page_size").GetInt32());
    Assert.Equal("full", next.GetProperty("detail").GetString());
    Assert.Equal("2026-07-01T01:02:03+00:00", next.GetProperty("since").GetString());
  }

  [Fact]
  public async Task ExerciseHistoryContinuationPreservesIdentityAndDateFilters()
  {
    var client = new FakeHevyClient { ExerciseHistory = new PagedResult<ExerciseHistoryEntry>(1, 2, []) };

    var result = await ExerciseReadTools.GetExerciseHistory(
        Services(client), "template-1", 1, 7, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 25), "compact", CancellationToken.None);
    var next = result.Structured().GetProperty("meta").GetProperty("continuation");

    Assert.Equal("template-1", next.GetProperty("exercise_template_id").GetString());
    Assert.Equal(2, next.GetProperty("page").GetInt32());
    Assert.Equal(7, next.GetProperty("page_size").GetInt32());
    Assert.Equal("2026-07-01", next.GetProperty("start_date").GetString());
    Assert.Equal("2026-07-25", next.GetProperty("end_date").GetString());
    Assert.Equal("compact", next.GetProperty("detail").GetString());
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
