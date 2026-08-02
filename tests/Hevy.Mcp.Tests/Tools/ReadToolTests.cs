using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class ReadToolTests
{
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
    var rejected = await ToolExceptionFilter.ExecuteAsync(() => ExerciseReadTools.GetExerciseTemplates(services, 1, 101, "compact", default));

    (accepted.IsError).Should().BeFalse();
    (acceptedPageSize).Should().Be(100);
    (rejected.IsError).Should().BeTrue();
    (rejected.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (client.CallCount).Should().Be(1);
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

    (client.Operations).Should().Equal([
      nameof(IHevyClient.GetWorkoutCountAsync),
      nameof(IHevyClient.GetWorkoutEventsAsync),
      nameof(IHevyClient.GetWorkoutAsync),
      nameof(IHevyClient.GetRoutinesAsync),
      nameof(IHevyClient.GetRoutineAsync),
      nameof(IHevyClient.GetRoutineFoldersAsync),
      nameof(IHevyClient.GetRoutineFolderAsync),
      nameof(IHevyClient.GetExerciseTemplatesAsync),
      nameof(IHevyClient.GetExerciseTemplateAsync),
      nameof(IHevyClient.GetExerciseHistoryWindowAsync),
      nameof(IHevyClient.GetBodyMeasurementsAsync),
      nameof(IHevyClient.GetBodyMeasurementAsync),
      nameof(IHevyClient.GetUserInfoAsync),
    ]);
  }

  [Fact]
  public async Task ExerciseHistoryRejectsAnInvertedDateRangeBeforeClientIo()
  {
    var client = new FakeHevyClient();

    var result = await ToolExceptionFilter.ExecuteAsync(() => ExerciseReadTools.GetExerciseHistory(
        Services(client), "template-1", 1, 10, new DateOnly(2026, 7, 25), new DateOnly(2026, 7, 1), "full", CancellationToken.None));

    (result.IsError).Should().BeTrue();
    (client.CallCount).Should().Be(0);
  }

  [Fact]
  public async Task ExerciseHistoryRejectsAPageBeyondTheStreamingScanBudgetBeforeClientIo()
  {
    var client = new FakeHevyClient();

    var result = await ToolExceptionFilter.ExecuteAsync(() => ExerciseReadTools.GetExerciseHistory(
        Services(client), "template-1", 101, 10, null, null, "full", CancellationToken.None));

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

    var result = await ToolExceptionFilter.ExecuteAsync(() => WorkoutReadTools.GetWorkout(Services(client), "missing", CancellationToken.None));
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

}
