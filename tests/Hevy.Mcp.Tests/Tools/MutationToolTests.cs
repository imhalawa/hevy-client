using Hevy.Core.Exceptions;
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

    var workout = await WorkoutWriteTools.CreateWorkout(services, FixtureFactory.Create<CreateWorkoutCommand>(), true, CancellationToken.None);
    var routine = await RoutineWriteTools.CreateRoutine(services, FixtureFactory.Create<CreateRoutineCommand>(), true, CancellationToken.None);
    var folder = await RoutineWriteTools.CreateRoutineFolder(services, FixtureFactory.Create<CreateRoutineFolderCommand>(), true, CancellationToken.None);
    var template = await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.Create<CreateExerciseTemplateCommand>(), true, CancellationToken.None);
    var measurement = await MeasurementWriteTools.CreateBodyMeasurement(services, FixtureFactory.Create<CreateBodyMeasurementCommand>(), true, CancellationToken.None);

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

    var workout = await WorkoutWriteTools.CreateWorkout(services, FixtureFactory.Create<CreateWorkoutCommand>(), false, CancellationToken.None);
    await RoutineWriteTools.CreateRoutine(services, FixtureFactory.Create<CreateRoutineCommand>(), false, CancellationToken.None);
    await RoutineWriteTools.CreateRoutineFolder(services, FixtureFactory.Create<CreateRoutineFolderCommand>(), false, CancellationToken.None);
    await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.Create<CreateExerciseTemplateCommand>(), false, CancellationToken.None);
    await MeasurementWriteTools.CreateBodyMeasurement(services, FixtureFactory.Create<CreateBodyMeasurementCommand>(), false, CancellationToken.None);

    (client.Operations).Should().Equal([
      nameof(IHevyClient.CreateWorkoutAsync),
      nameof(IHevyClient.CreateRoutineAsync),
      nameof(IHevyClient.CreateRoutineFolderAsync),
      nameof(IHevyClient.CreateExerciseTemplateAsync),
      nameof(IHevyClient.CreateBodyMeasurementAsync),
    ]);
    (workout.Structured().GetProperty("data").GetProperty("result").GetProperty("id").GetString()).Should().Be("workout-1");
  }

  [Fact]
  public async Task InvalidMutationPayloadReturnsValidationErrorBeforeClientIo()
  {
    var client = new FakeHevyClient();
    var invalid = FixtureFactory.Create<CreateWorkoutCommand>() with
    {
      Workout = FixtureFactory.Create<CreateWorkoutCommand>().Workout with { Title = " " },
    };

    var result = await ToolExceptionFilter.ExecuteAsync(() => WorkoutWriteTools.CreateWorkout(Services(client), invalid, false, CancellationToken.None));

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (client.CallCount).Should().Be(0);
  }

  [Fact]
  public async Task EveryMutationFamilyValidatesLocallyBeforeAnyClientIo()
  {
    var client = new FakeHevyClient();
    var services = Services(client);
    var createWorkout = FixtureFactory.Create<CreateWorkoutCommand>() with { Workout = FixtureFactory.Create<CreateWorkoutCommand>().Workout with { Title = "" } };
    var updateWorkout = FixtureFactory.Create<UpdateWorkoutCommand>() with { Workout = FixtureFactory.Create<UpdateWorkoutCommand>().Workout with { Title = "" } };
    var createRoutine = FixtureFactory.Create<CreateRoutineCommand>() with { Routine = FixtureFactory.Create<CreateRoutineCommand>().Routine with { Title = "" } };
    var updateRoutine = FixtureFactory.Create<UpdateRoutineCommand>() with { Routine = FixtureFactory.Create<UpdateRoutineCommand>().Routine with { Title = "" } };
    var folder = new CreateRoutineFolderCommand(new RoutineFolderWrite(""));
    var template = FixtureFactory.Create<CreateExerciseTemplateCommand>() with { Exercise = FixtureFactory.Create<CreateExerciseTemplateCommand>().Exercise with { Title = "" } };
    var createMeasurement = FixtureFactory.Create<CreateBodyMeasurementCommand>();
    createMeasurement = createMeasurement with { Measurement = createMeasurement.Measurement with { WeightKg = -1 } };
    var updateMeasurement = FixtureFactory.Create<UpdateBodyMeasurementCommand>();
    updateMeasurement = updateMeasurement with { Measurement = updateMeasurement.Measurement with { WeightKg = -1 } };

    var results = new[]
    {
      await ToolExceptionFilter.ExecuteAsync(() => WorkoutWriteTools.CreateWorkout(services, createWorkout, false, CancellationToken.None)),
      await ToolExceptionFilter.ExecuteAsync(() => WorkoutWriteTools.UpdateWorkout(services, "workout-1", updateWorkout, null, true, false, CancellationToken.None)),
      await ToolExceptionFilter.ExecuteAsync(() => RoutineWriteTools.CreateRoutine(services, createRoutine, false, CancellationToken.None)),
      await ToolExceptionFilter.ExecuteAsync(() => RoutineWriteTools.UpdateRoutine(services, "routine-1", updateRoutine, null, true, false, CancellationToken.None)),
      await ToolExceptionFilter.ExecuteAsync(() => RoutineWriteTools.CreateRoutineFolder(services, folder, false, CancellationToken.None)),
      await ToolExceptionFilter.ExecuteAsync(() => ExerciseWriteTools.CreateExerciseTemplate(services, template, false, CancellationToken.None)),
      await ToolExceptionFilter.ExecuteAsync(() => MeasurementWriteTools.CreateBodyMeasurement(services, createMeasurement, false, CancellationToken.None)),
      await ToolExceptionFilter.ExecuteAsync(() => MeasurementWriteTools.UpdateBodyMeasurement(services, new DateOnly(2024, 8, 14), updateMeasurement, null, true, false, CancellationToken.None)),
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
        Services(client), "workout-1", FixtureFactory.Create<UpdateWorkoutCommand>(), expected, false, false, CancellationToken.None);

    (result.IsError).Should().BeFalse();
    (client.Operations).Should().Equal([nameof(IHevyClient.GetWorkoutAsync), nameof(IHevyClient.UpdateWorkoutAsync)]);
    (result.Structured().GetProperty("meta").GetProperty("forced").GetBoolean()).Should().BeFalse();
  }

  [Fact]
  public async Task GuardedRoutineUpdateReturnsSafeConflictWithoutWritingWhenTimestampChanged()
  {
    var client = new FakeHevyClient();

    var result = await ToolExceptionFilter.ExecuteAsync(() => RoutineWriteTools.UpdateRoutine(
        Services(client), "routine-1", FixtureFactory.Create<UpdateRoutineCommand>(), DateTimeOffset.Parse("2026-07-24T12:00:00Z"), false, false, CancellationToken.None));

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("conflict");
    (client.Operations).Should().Equal([nameof(IHevyClient.GetRoutineAsync)]);
  }

  [Fact]
  public async Task ForcedUpdatesExplicitlyBypassGuardsAndMakeOneWriteCallEach()
  {
    var workoutClient = new FakeHevyClient();
    var routineClient = new FakeHevyClient();
    var measurementClient = new FakeHevyClient();

    var workout = await WorkoutWriteTools.UpdateWorkout(Services(workoutClient), "workout-1", FixtureFactory.Create<UpdateWorkoutCommand>(), null, true, false, CancellationToken.None);
    var routine = await RoutineWriteTools.UpdateRoutine(Services(routineClient), "routine-1", FixtureFactory.Create<UpdateRoutineCommand>(), null, true, false, CancellationToken.None);
    var measurement = await MeasurementWriteTools.UpdateBodyMeasurement(Services(measurementClient), new DateOnly(2024, 8, 14), FixtureFactory.Create<UpdateBodyMeasurementCommand>(), null, true, false, CancellationToken.None);

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

    var workout = await WorkoutWriteTools.UpdateWorkout(services, "workout-1", FixtureFactory.Create<UpdateWorkoutCommand>(), null, true, true, CancellationToken.None);
    var routine = await RoutineWriteTools.UpdateRoutine(services, "routine-1", FixtureFactory.Create<UpdateRoutineCommand>(), null, true, true, CancellationToken.None);
    var measurement = await MeasurementWriteTools.UpdateBodyMeasurement(services, new DateOnly(2024, 8, 14), FixtureFactory.Create<UpdateBodyMeasurementCommand>(), null, true, true, CancellationToken.None);

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
        Services(client), new DateOnly(2024, 8, 14), FixtureFactory.Create<UpdateBodyMeasurementCommand>(), DateTimeOffset.Parse("2024-08-14T12:00:00Z"), false, false, CancellationToken.None);

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("conflict");
    (result.Structured().GetProperty("meta").GetProperty("guard_available").GetBoolean()).Should().BeFalse();
    (result.Structured().GetProperty("meta").GetProperty("guard_limitation").GetString()).Should().Contain("do not expose updated_at");
    (client.CallCount).Should().Be(1);
    (client.LastOperation).Should().Be(nameof(IHevyClient.GetBodyMeasurementAsync));
  }

  private static IServiceProvider Services(IHevyClient client) => new ServiceCollection()
      .AddSingleton(client)
      .BuildServiceProvider();

}
