using Hevy.Client;
using Hevy.Client.Errors;
using Hevy.Client.Models;
using Hevy.Mcp.Caching;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class MutationToolTests
{
  [Fact]
  public async Task EveryCreateDryRunReturnsItsExactNormalizedPayloadWithoutClientIo()
  {
    var client = new FakeHevyClient();
    var services = Services(client);

    var workout = await WorkoutWriteTools.CreateWorkout(services, FixtureFactory.CreateWorkoutRequest(), true, CancellationToken.None);
    var routine = await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineRequest(), true, CancellationToken.None);
    var folder = await RoutineWriteTools.CreateRoutineFolder(services, FixtureFactory.CreateRoutineFolderRequest(), true, CancellationToken.None);
    var template = await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateRequest(), true, CancellationToken.None);
    var measurement = await MeasurementWriteTools.CreateBodyMeasurement(services, FixtureFactory.CreateBodyMeasurementRequest(), true, CancellationToken.None);

    Assert.Equal("Friday Leg Day", workout.Structured().GetProperty("data").GetProperty("payload").GetProperty("workout").GetProperty("title").GetString());
    Assert.Equal("D04AC939", workout.Structured().GetProperty("data").GetProperty("payload").GetProperty("workout").GetProperty("exercises")[0].GetProperty("exercise_template_id").GetString());
    Assert.Equal("April Leg Day", routine.Structured().GetProperty("data").GetProperty("payload").GetProperty("routine").GetProperty("title").GetString());
    Assert.Equal("Push Pull", folder.Structured().GetProperty("data").GetProperty("payload").GetProperty("routine_folder").GetProperty("title").GetString());
    Assert.Equal("weight_reps", template.Structured().GetProperty("data").GetProperty("payload").GetProperty("exercise").GetProperty("exercise_type").GetString());
    Assert.Equal("2024-08-14", measurement.Structured().GetProperty("data").GetProperty("payload").GetProperty("date").GetString());
    Assert.All(new[] { workout, routine, folder, template, measurement }, result =>
    {
      Assert.True(result.Structured().GetProperty("meta").GetProperty("dry_run").GetBoolean());
      Assert.Empty(result.Structured().GetProperty("meta").GetProperty("validation_warnings").EnumerateArray());
    });
    Assert.Equal(0, client.CallCount);
  }

  [Fact]
  public async Task EveryCreateMakesExactlyOneMatchingClientCallWhenNotDryRun()
  {
    var client = new FakeHevyClient();
    var services = Services(client);

    var workout = await WorkoutWriteTools.CreateWorkout(services, FixtureFactory.CreateWorkoutRequest(), false, CancellationToken.None);
    await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineRequest(), false, CancellationToken.None);
    await RoutineWriteTools.CreateRoutineFolder(services, FixtureFactory.CreateRoutineFolderRequest(), false, CancellationToken.None);
    await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateRequest(), false, CancellationToken.None);
    await MeasurementWriteTools.CreateBodyMeasurement(services, FixtureFactory.CreateBodyMeasurementRequest(), false, CancellationToken.None);

    Assert.Equal(5, client.CallCount);
    Assert.Equal(nameof(IHevyClient.CreateBodyMeasurementAsync), client.LastOperation);
    Assert.Equal("workout-1", workout.Structured().GetProperty("data").GetProperty("result").GetProperty("id").GetString());
  }

  [Fact]
  public async Task InvalidMutationPayloadReturnsValidationErrorBeforeClientIo()
  {
    var client = new FakeHevyClient();
    var invalid = FixtureFactory.CreateWorkoutRequest() with
    {
      Workout = FixtureFactory.CreateWorkoutRequest().Workout with { Title = " " },
    };

    var result = await WorkoutWriteTools.CreateWorkout(Services(client), invalid, false, CancellationToken.None);

    Assert.True(result.IsError);
    Assert.Equal("validation_error", result.Structured().GetProperty("error").GetProperty("code").GetString());
    Assert.Equal(0, client.CallCount);
  }

  [Fact]
  public async Task EveryMutationFamilyValidatesLocallyBeforeAnyClientIo()
  {
    var client = new FakeHevyClient();
    var services = Services(client);
    var createWorkout = FixtureFactory.CreateWorkoutRequest() with { Workout = FixtureFactory.CreateWorkoutRequest().Workout with { Title = "" } };
    var updateWorkout = FixtureFactory.UpdateWorkoutRequest() with { Workout = FixtureFactory.UpdateWorkoutRequest().Workout with { Title = "" } };
    var createRoutine = FixtureFactory.CreateRoutineRequest() with { Routine = FixtureFactory.CreateRoutineRequest().Routine with { Title = "" } };
    var updateRoutine = FixtureFactory.UpdateRoutineRequest() with { Routine = FixtureFactory.UpdateRoutineRequest().Routine with { Title = "" } };
    var folder = new CreateRoutineFolderRequest(new RoutineFolderWrite(""));
    var template = FixtureFactory.CreateExerciseTemplateRequest() with { Exercise = FixtureFactory.CreateExerciseTemplateRequest().Exercise with { Title = "" } };
    var createMeasurement = FixtureFactory.CreateBodyMeasurementRequest() with { WeightKg = -1 };
    var updateMeasurement = FixtureFactory.UpdateBodyMeasurementRequest() with { WeightKg = -1 };

    var results = new[]
    {
      await WorkoutWriteTools.CreateWorkout(services, createWorkout, false, CancellationToken.None),
      await WorkoutWriteTools.UpdateWorkout(services, "workout-1", updateWorkout, null, true, false, CancellationToken.None),
      await RoutineWriteTools.CreateRoutine(services, createRoutine, false, CancellationToken.None),
      await RoutineWriteTools.UpdateRoutine(services, "routine-1", updateRoutine, null, true, false, CancellationToken.None),
      await RoutineWriteTools.CreateRoutineFolder(services, folder, false, CancellationToken.None),
      await ExerciseWriteTools.CreateExerciseTemplate(services, template, false, CancellationToken.None),
      await MeasurementWriteTools.CreateBodyMeasurement(services, createMeasurement, false, CancellationToken.None),
      await MeasurementWriteTools.UpdateBodyMeasurement(services, new DateOnly(2024, 8, 14), updateMeasurement, null, true, false, CancellationToken.None),
    };

    Assert.All(results, result => Assert.Equal("validation_error", result.Structured().GetProperty("error").GetProperty("code").GetString()));
    Assert.Equal(0, client.CallCount);
  }

  [Fact]
  public async Task GuardedWorkoutUpdateFetchesCurrentStateThenWritesOnExactTimestampMatch()
  {
    var client = new FakeHevyClient();
    var expected = FakeHevyClient.SampleWorkout().UpdatedAt;

    var result = await WorkoutWriteTools.UpdateWorkout(
        Services(client), "workout-1", FixtureFactory.UpdateWorkoutRequest(), expected, false, false, CancellationToken.None);

    Assert.False(result.IsError);
    Assert.Equal(2, client.CallCount);
    Assert.Equal(nameof(IHevyClient.UpdateWorkoutAsync), client.LastOperation);
    Assert.False(result.Structured().GetProperty("meta").GetProperty("forced").GetBoolean());
  }

  [Fact]
  public async Task GuardedRoutineUpdateReturnsSafeConflictWithoutWritingWhenTimestampChanged()
  {
    var client = new FakeHevyClient();

    var result = await RoutineWriteTools.UpdateRoutine(
        Services(client), "routine-1", FixtureFactory.UpdateRoutineRequest(), DateTimeOffset.Parse("2026-07-24T12:00:00Z"), false, false, CancellationToken.None);

    Assert.True(result.IsError);
    Assert.Equal("conflict", result.Structured().GetProperty("error").GetProperty("code").GetString());
    Assert.Equal(1, client.CallCount);
    Assert.Equal(nameof(IHevyClient.GetRoutineAsync), client.LastOperation);
  }

  [Fact]
  public async Task ForcedUpdatesExplicitlyBypassGuardsAndMakeOneWriteCallEach()
  {
    var workoutClient = new FakeHevyClient();
    var routineClient = new FakeHevyClient();
    var measurementClient = new FakeHevyClient();

    var workout = await WorkoutWriteTools.UpdateWorkout(Services(workoutClient), "workout-1", FixtureFactory.UpdateWorkoutRequest(), null, true, false, CancellationToken.None);
    var routine = await RoutineWriteTools.UpdateRoutine(Services(routineClient), "routine-1", FixtureFactory.UpdateRoutineRequest(), null, true, false, CancellationToken.None);
    var measurement = await MeasurementWriteTools.UpdateBodyMeasurement(Services(measurementClient), new DateOnly(2024, 8, 14), FixtureFactory.UpdateBodyMeasurementRequest(), null, true, false, CancellationToken.None);

    Assert.Equal(1, workoutClient.CallCount);
    Assert.Equal(1, routineClient.CallCount);
    Assert.Equal(1, measurementClient.CallCount);
    Assert.True(workout.Structured().GetProperty("meta").GetProperty("forced").GetBoolean());
    Assert.True(routine.Structured().GetProperty("meta").GetProperty("forced").GetBoolean());
    Assert.True(measurement.Structured().GetProperty("meta").GetProperty("forced").GetBoolean());
    Assert.False(measurement.Structured().GetProperty("meta").GetProperty("guard_available").GetBoolean());
    Assert.Contains("do not expose updated_at", measurement.Structured().GetProperty("meta").GetProperty("guard_limitation").GetString(), StringComparison.Ordinal);
  }

  [Fact]
  public async Task EveryUpdateDryRunReturnsPayloadAndMakesZeroClientCalls()
  {
    var client = new FakeHevyClient();
    var services = Services(client);

    var workout = await WorkoutWriteTools.UpdateWorkout(services, "workout-1", FixtureFactory.UpdateWorkoutRequest(), null, true, true, CancellationToken.None);
    var routine = await RoutineWriteTools.UpdateRoutine(services, "routine-1", FixtureFactory.UpdateRoutineRequest(), null, true, true, CancellationToken.None);
    var measurement = await MeasurementWriteTools.UpdateBodyMeasurement(services, new DateOnly(2024, 8, 14), FixtureFactory.UpdateBodyMeasurementRequest(), null, true, true, CancellationToken.None);

    Assert.Equal("Friday Leg Day", workout.Structured().GetProperty("data").GetProperty("payload").GetProperty("workout").GetProperty("title").GetString());
    Assert.Equal("April Leg Day", routine.Structured().GetProperty("data").GetProperty("payload").GetProperty("routine").GetProperty("title").GetString());
    Assert.Equal(80.5m, measurement.Structured().GetProperty("data").GetProperty("payload").GetProperty("weight_kg").GetDecimal());
    Assert.Equal(0, client.CallCount);
  }

  [Fact]
  public async Task MeasurementGuardReturnsConflictBecauseHevyExposesNoUpdatedTimestamp()
  {
    var client = new FakeHevyClient();

    var result = await MeasurementWriteTools.UpdateBodyMeasurement(
        Services(client), new DateOnly(2024, 8, 14), FixtureFactory.UpdateBodyMeasurementRequest(), DateTimeOffset.Parse("2024-08-14T12:00:00Z"), false, false, CancellationToken.None);

    Assert.True(result.IsError);
    Assert.Equal("conflict", result.Structured().GetProperty("error").GetProperty("code").GetString());
    Assert.False(result.Structured().GetProperty("meta").GetProperty("guard_available").GetBoolean());
    Assert.Contains("do not expose updated_at", result.Structured().GetProperty("meta").GetProperty("guard_limitation").GetString(), StringComparison.Ordinal);
    Assert.Equal(1, client.CallCount);
    Assert.Equal(nameof(IHevyClient.GetBodyMeasurementAsync), client.LastOperation);
  }

  [Fact]
  public async Task SuccessfulRelatedMutationsInvalidateCatalogsButDryRunsDoNot()
  {
    var client = new FakeHevyClient
    {
      Routines = new(1, 1, [FakeHevyClient.SampleRoutine()]),
      ExerciseTemplates = new(1, 1, [new ExerciseTemplate("template-1", "Squat", "weight_reps", "quadriceps", ["glutes"], EquipmentCategory.Barbell, false)]),
    };
    var collection = new ServiceCollection()
        .AddSingleton<IHevyClient>(client)
        .AddMemoryCache(memory => memory.SizeLimit = 2)
        .AddSingleton(TimeProvider.System)
        .AddSingleton<HevyCache>();
    using var services = collection.BuildServiceProvider();
    var cache = services.GetRequiredService<HevyCache>();
    await cache.GetRoutinesAsync(default);
    await cache.GetExerciseTemplatesAsync(default);

    await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineRequest(), true, default);
    await cache.GetRoutinesAsync(default);
    Assert.Equal(2, client.CallCount);

    await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineRequest(), false, default);
    await cache.GetRoutinesAsync(default);
    await cache.GetExerciseTemplatesAsync(default);
    Assert.Equal(4, client.CallCount);

    await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateRequest(), false, default);
    await cache.GetExerciseTemplatesAsync(default);
    Assert.Equal(6, client.CallCount);
  }

  // Break caught: a committed exercise creation whose read-back fails leaving the stale catalog cached.
  [Fact]
  public async Task Committed_exercise_with_failed_readback_invalidates_the_template_cache()
  {
    var client = new FakeHevyClient
    {
      ExerciseTemplates = new(1, 1, [new ExerciseTemplate("template-1", "Squat", "weight_reps", "quadriceps", ["glutes"], EquipmentCategory.Barbell, false)]),
      CreateExerciseTemplateHandler = (_, _) => Task.FromException<ExerciseTemplate>(new HevyCommittedReadbackException()),
    };
    var collection = new ServiceCollection()
        .AddSingleton<IHevyClient>(client)
        .AddMemoryCache(memory => memory.SizeLimit = 2)
        .AddSingleton(TimeProvider.System)
        .AddSingleton<HevyCache>();
    using var services = collection.BuildServiceProvider();
    var cache = services.GetRequiredService<HevyCache>();
    await cache.GetExerciseTemplatesAsync(default);

    var result = await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateRequest(), false, default);
    await cache.GetExerciseTemplatesAsync(default);

    Assert.True(result.IsError);
    Assert.Equal("committed_readback_failed", result.Structured().GetProperty("error").GetProperty("code").GetString());
    Assert.False(result.Structured().GetProperty("error").GetProperty("retryable").GetBoolean());
    Assert.Equal(3, client.CallCount);
  }

  // Break caught: a confirmed routine write whose response cannot be read leaving a stale pre-write catalog in memory.
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Every_routine_mutation_invalidates_before_a_possible_post_commit_failure(bool update)
  {
    var client = new FakeHevyClient
    {
      Routines = new(1, 1, [FakeHevyClient.SampleRoutine()]),
      CreateRoutineHandler = (_, _) => Task.FromException<Routine>(new HevyCommittedReadbackException()),
      UpdateRoutineHandler = (_, _, _) => Task.FromException<Routine>(new HevyCommittedReadbackException()),
    };
    using var services = CachedServices(client);
    var cache = services.GetRequiredService<HevyCache>();
    await cache.GetRoutinesAsync(default);

    var result = update
        ? await RoutineWriteTools.UpdateRoutine(services, "routine-1", FixtureFactory.UpdateRoutineRequest(), null, true, false, default)
        : await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineRequest(), false, default);
    await cache.GetRoutinesAsync(default);

    Assert.True(result.IsError);
    Assert.Equal("committed_readback_failed", result.Structured().GetProperty("error").GetProperty("code").GetString());
    Assert.Equal(3, client.CallCount);
  }

  // Break caught: caller cancellation after a committed custom-exercise write retaining a stale template catalog.
  [Fact]
  public async Task Template_mutation_invalidates_before_a_cancelled_post_commit_readback()
  {
    using var cancellation = new CancellationTokenSource();
    var client = new FakeHevyClient
    {
      ExerciseTemplates = new(1, 1, [new ExerciseTemplate("template-1", "Squat", "weight_reps", "quadriceps", ["glutes"], EquipmentCategory.Barbell, false)]),
      CreateExerciseTemplateHandler = (_, token) =>
      {
        cancellation.Cancel();
        return Task.FromCanceled<ExerciseTemplate>(token);
      },
    };
    using var services = CachedServices(client);
    var cache = services.GetRequiredService<HevyCache>();
    await cache.GetExerciseTemplatesAsync(default);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateRequest(), false, cancellation.Token));
    await cache.GetExerciseTemplatesAsync(default);

    Assert.Equal(3, client.CallCount);
  }

  private static IServiceProvider Services(IHevyClient client) => new ServiceCollection()
      .AddSingleton(client)
      .BuildServiceProvider();

  private static ServiceProvider CachedServices(IHevyClient client) => new ServiceCollection()
      .AddSingleton(client)
      .AddMemoryCache(memory => memory.SizeLimit = 2)
      .AddSingleton(TimeProvider.System)
      .AddSingleton<HevyCache>()
      .BuildServiceProvider();
}
