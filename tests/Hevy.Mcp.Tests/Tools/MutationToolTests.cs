using Hevy.Client;
using Hevy.Core.Exceptions;
using Hevy.Core.Models;
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

    var workout = await WorkoutWriteTools.CreateWorkout(services, FixtureFactory.CreateWorkoutCommand(), true, CancellationToken.None);
    var routine = await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineCommand(), true, CancellationToken.None);
    var folder = await RoutineWriteTools.CreateRoutineFolder(services, FixtureFactory.CreateRoutineFolderCommand(), true, CancellationToken.None);
    var template = await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateCommand(), true, CancellationToken.None);
    var measurement = await MeasurementWriteTools.CreateBodyMeasurement(services, FixtureFactory.NewBodyMeasurement(), true, CancellationToken.None);

    (workout.Structured().GetProperty("data").GetProperty("payload").GetProperty("workout").GetProperty("title").GetString()).Should().Be("Friday Leg Day");
    (workout.Structured().GetProperty("data").GetProperty("payload").GetProperty("workout").GetProperty("exercises")[0].GetProperty("exercise_template_id").GetString()).Should().Be("D04AC939");
    (routine.Structured().GetProperty("data").GetProperty("payload").GetProperty("routine").GetProperty("title").GetString()).Should().Be("April Leg Day");
    (folder.Structured().GetProperty("data").GetProperty("payload").GetProperty("routine_folder").GetProperty("title").GetString()).Should().Be("Push Pull");
    (template.Structured().GetProperty("data").GetProperty("payload").GetProperty("exercise").GetProperty("exercise_type").GetString()).Should().Be("weight_reps");
    (measurement.Structured().GetProperty("data").GetProperty("payload").GetProperty("date").GetString()).Should().Be("2024-08-14");
    (new[] { workout, routine, folder, template, measurement }).Should().AllSatisfy(result =>
    {
      (result.Structured().GetProperty("meta").GetProperty("dry_run").GetBoolean()).Should().BeTrue();
      (result.Structured().GetProperty("meta").GetProperty("validation_warnings").EnumerateArray()).Should().BeEmpty();
    });
    (client.CallCount).Should().Be(0);
  }

  [Fact]
  public async Task EveryCreateMakesExactlyOneMatchingClientCallWhenNotDryRun()
  {
    var client = new FakeHevyClient();
    var services = Services(client);

    var workout = await WorkoutWriteTools.CreateWorkout(services, FixtureFactory.CreateWorkoutCommand(), false, CancellationToken.None);
    await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineCommand(), false, CancellationToken.None);
    await RoutineWriteTools.CreateRoutineFolder(services, FixtureFactory.CreateRoutineFolderCommand(), false, CancellationToken.None);
    await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateCommand(), false, CancellationToken.None);
    await MeasurementWriteTools.CreateBodyMeasurement(services, FixtureFactory.NewBodyMeasurement(), false, CancellationToken.None);

    (client.CallCount).Should().Be(5);
    (client.LastOperation).Should().Be(nameof(IHevyClient.CreateBodyMeasurementAsync));
    (workout.Structured().GetProperty("data").GetProperty("result").GetProperty("id").GetString()).Should().Be("workout-1");
  }

  [Fact]
  public async Task InvalidMutationPayloadReturnsValidationErrorBeforeClientIo()
  {
    var client = new FakeHevyClient();
    var invalid = FixtureFactory.CreateWorkoutCommand() with
    {
      Workout = FixtureFactory.CreateWorkoutCommand().Workout with { Title = " " },
    };

    var result = await WorkoutWriteTools.CreateWorkout(Services(client), invalid, false, CancellationToken.None);

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (client.CallCount).Should().Be(0);
  }

  [Fact]
  public async Task EveryMutationFamilyValidatesLocallyBeforeAnyClientIo()
  {
    var client = new FakeHevyClient();
    var services = Services(client);
    var createWorkout = FixtureFactory.CreateWorkoutCommand() with { Workout = FixtureFactory.CreateWorkoutCommand().Workout with { Title = "" } };
    var updateWorkout = FixtureFactory.UpdateWorkoutCommand() with { Workout = FixtureFactory.UpdateWorkoutCommand().Workout with { Title = "" } };
    var createRoutine = FixtureFactory.CreateRoutineCommand() with { Routine = FixtureFactory.CreateRoutineCommand().Routine with { Title = "" } };
    var updateRoutine = FixtureFactory.UpdateRoutineCommand() with { Routine = FixtureFactory.UpdateRoutineCommand().Routine with { Title = "" } };
    var folder = new CreateRoutineFolderCommand(new RoutineFolderWrite(""));
    var template = FixtureFactory.CreateExerciseTemplateCommand() with { Exercise = FixtureFactory.CreateExerciseTemplateCommand().Exercise with { Title = "" } };
    var createMeasurement = FixtureFactory.NewBodyMeasurement() with { WeightKg = -1 };
    var updateMeasurement = FixtureFactory.BodyMeasurementUpdate() with { WeightKg = -1 };

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

    (results).Should().AllSatisfy(result => (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error"));
    (client.CallCount).Should().Be(0);
  }

  [Fact]
  public async Task GuardedWorkoutUpdateFetchesCurrentStateThenWritesOnExactTimestampMatch()
  {
    var client = new FakeHevyClient();
    var expected = FakeHevyClient.SampleWorkout().UpdatedAt;

    var result = await WorkoutWriteTools.UpdateWorkout(
        Services(client), "workout-1", FixtureFactory.UpdateWorkoutCommand(), expected, false, false, CancellationToken.None);

    (result.IsError).Should().BeFalse();
    (client.CallCount).Should().Be(2);
    (client.LastOperation).Should().Be(nameof(IHevyClient.UpdateWorkoutAsync));
    (result.Structured().GetProperty("meta").GetProperty("forced").GetBoolean()).Should().BeFalse();
  }

  [Fact]
  public async Task GuardedRoutineUpdateReturnsSafeConflictWithoutWritingWhenTimestampChanged()
  {
    var client = new FakeHevyClient();

    var result = await RoutineWriteTools.UpdateRoutine(
        Services(client), "routine-1", FixtureFactory.UpdateRoutineCommand(), DateTimeOffset.Parse("2026-07-24T12:00:00Z"), false, false, CancellationToken.None);

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("conflict");
    (client.CallCount).Should().Be(1);
    (client.LastOperation).Should().Be(nameof(IHevyClient.GetRoutineAsync));
  }

  [Fact]
  public async Task ForcedUpdatesExplicitlyBypassGuardsAndMakeOneWriteCallEach()
  {
    var workoutClient = new FakeHevyClient();
    var routineClient = new FakeHevyClient();
    var measurementClient = new FakeHevyClient();

    var workout = await WorkoutWriteTools.UpdateWorkout(Services(workoutClient), "workout-1", FixtureFactory.UpdateWorkoutCommand(), null, true, false, CancellationToken.None);
    var routine = await RoutineWriteTools.UpdateRoutine(Services(routineClient), "routine-1", FixtureFactory.UpdateRoutineCommand(), null, true, false, CancellationToken.None);
    var measurement = await MeasurementWriteTools.UpdateBodyMeasurement(Services(measurementClient), new DateOnly(2024, 8, 14), FixtureFactory.BodyMeasurementUpdate(), null, true, false, CancellationToken.None);

    (workoutClient.CallCount).Should().Be(1);
    (routineClient.CallCount).Should().Be(1);
    (measurementClient.CallCount).Should().Be(1);
    (workout.Structured().GetProperty("meta").GetProperty("forced").GetBoolean()).Should().BeTrue();
    (routine.Structured().GetProperty("meta").GetProperty("forced").GetBoolean()).Should().BeTrue();
    (measurement.Structured().GetProperty("meta").GetProperty("forced").GetBoolean()).Should().BeTrue();
    (measurement.Structured().GetProperty("meta").GetProperty("guard_available").GetBoolean()).Should().BeFalse();
    (measurement.Structured().GetProperty("meta").GetProperty("guard_limitation").GetString()).Should().Contain("do not expose updated_at");
  }

  [Fact]
  public async Task EveryUpdateDryRunReturnsPayloadAndMakesZeroClientCalls()
  {
    var client = new FakeHevyClient();
    var services = Services(client);

    var workout = await WorkoutWriteTools.UpdateWorkout(services, "workout-1", FixtureFactory.UpdateWorkoutCommand(), null, true, true, CancellationToken.None);
    var routine = await RoutineWriteTools.UpdateRoutine(services, "routine-1", FixtureFactory.UpdateRoutineCommand(), null, true, true, CancellationToken.None);
    var measurement = await MeasurementWriteTools.UpdateBodyMeasurement(services, new DateOnly(2024, 8, 14), FixtureFactory.BodyMeasurementUpdate(), null, true, true, CancellationToken.None);

    (workout.Structured().GetProperty("data").GetProperty("payload").GetProperty("workout").GetProperty("title").GetString()).Should().Be("Friday Leg Day");
    (routine.Structured().GetProperty("data").GetProperty("payload").GetProperty("routine").GetProperty("title").GetString()).Should().Be("April Leg Day");
    (measurement.Structured().GetProperty("data").GetProperty("payload").GetProperty("weight_kg").GetDecimal()).Should().Be(80.5m);
    (client.CallCount).Should().Be(0);
  }

  [Fact]
  public async Task MeasurementGuardReturnsConflictBecauseHevyExposesNoUpdatedTimestamp()
  {
    var client = new FakeHevyClient();

    var result = await MeasurementWriteTools.UpdateBodyMeasurement(
        Services(client), new DateOnly(2024, 8, 14), FixtureFactory.BodyMeasurementUpdate(), DateTimeOffset.Parse("2024-08-14T12:00:00Z"), false, false, CancellationToken.None);

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("conflict");
    (result.Structured().GetProperty("meta").GetProperty("guard_available").GetBoolean()).Should().BeFalse();
    (result.Structured().GetProperty("meta").GetProperty("guard_limitation").GetString()).Should().Contain("do not expose updated_at");
    (client.CallCount).Should().Be(1);
    (client.LastOperation).Should().Be(nameof(IHevyClient.GetBodyMeasurementAsync));
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

    await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineCommand(), true, default);
    await cache.GetRoutinesAsync(default);
    (client.CallCount).Should().Be(2);

    await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineCommand(), false, default);
    await cache.GetRoutinesAsync(default);
    await cache.GetExerciseTemplatesAsync(default);
    (client.CallCount).Should().Be(4);

    await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateCommand(), false, default);
    await cache.GetExerciseTemplatesAsync(default);
    (client.CallCount).Should().Be(6);
  }

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

    var result = await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateCommand(), false, default);
    await cache.GetExerciseTemplatesAsync(default);

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("committed_readback_failed");
    (result.Structured().GetProperty("error").GetProperty("retryable").GetBoolean()).Should().BeFalse();
    (client.CallCount).Should().Be(3);
  }

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
        ? await RoutineWriteTools.UpdateRoutine(services, "routine-1", FixtureFactory.UpdateRoutineCommand(), null, true, false, default)
        : await RoutineWriteTools.CreateRoutine(services, FixtureFactory.CreateRoutineCommand(), false, default);
    await cache.GetRoutinesAsync(default);

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("committed_readback_failed");
    (client.CallCount).Should().Be(3);
  }

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

    await FluentActions.Awaiting(() =>
        ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.CreateExerciseTemplateCommand(), false, cancellation.Token)).Should().ThrowAsync<OperationCanceledException>();
    await cache.GetExerciseTemplatesAsync(default);

    (client.CallCount).Should().Be(3);
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
