using Hevy.Client;
using Hevy.Client.Models;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class ReadToolTests
{
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
}
