using System.Reflection;
using System.Text.Json;
using Hevy.Mcp.Diagnostics;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class ToolOutputSchemaTests
{
  [Fact]
  public async Task EveryOperationStructuredContentMatchesItsAdvertisedOutputSchema()
  {
    var client = CompleteClient();
    var services = new ServiceCollection()
        .AddSingleton<IHevyClient>(client)
        .AddSingleton(TimeProvider.System)
        .AddSingleton<TrainingAnalysisUseCase>()
        .AddSingleton(new DiagnosticSnapshot("test-version", "test-runtime", "stdio", false, false, "ready"))
        .BuildServiceProvider();
    var since = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
    var start = new DateOnly(2026, 7, 1);
    var end = new DateOnly(2026, 7, 25);
    var cases = new List<(Type Type, string Method, CallToolResult Result)>
    {
      (typeof(WorkoutReadTools), nameof(WorkoutReadTools.GetWorkouts), await WorkoutReadTools.GetWorkouts(services, 1, 10, "full", default)),
      (typeof(WorkoutReadTools), nameof(WorkoutReadTools.GetWorkoutCount), await WorkoutReadTools.GetWorkoutCount(services, default)),
      (typeof(WorkoutReadTools), nameof(WorkoutReadTools.GetWorkoutEvents), await WorkoutReadTools.GetWorkoutEvents(services, 1, 10, since, "full", default)),
      (typeof(WorkoutReadTools), nameof(WorkoutReadTools.GetWorkout), await WorkoutReadTools.GetWorkout(services, "workout-1", default)),
      (typeof(WorkoutWriteTools), nameof(WorkoutWriteTools.CreateWorkout), await WorkoutWriteTools.CreateWorkout(services, FixtureFactory.Create<CreateWorkoutCommand>(), true, default)),
      (typeof(WorkoutWriteTools), nameof(WorkoutWriteTools.UpdateWorkout), await WorkoutWriteTools.UpdateWorkout(services, "workout-1", FixtureFactory.Create<UpdateWorkoutCommand>(), null, true, true, default)),
      (typeof(RoutineReadTools), nameof(RoutineReadTools.GetRoutines), await RoutineReadTools.GetRoutines(services, 1, 10, "full", default)),
      (typeof(RoutineReadTools), nameof(RoutineReadTools.GetRoutine), await RoutineReadTools.GetRoutine(services, "routine-1", default)),
      (typeof(RoutineReadTools), nameof(RoutineReadTools.GetRoutineFolders), await RoutineReadTools.GetRoutineFolders(services, 1, 10, "full", default)),
      (typeof(RoutineReadTools), nameof(RoutineReadTools.GetRoutineFolder), await RoutineReadTools.GetRoutineFolder(services, 1, default)),
      (typeof(RoutineWriteTools), nameof(RoutineWriteTools.CreateRoutine), await RoutineWriteTools.CreateRoutine(services, FixtureFactory.Create<CreateRoutineCommand>(), true, default)),
      (typeof(RoutineWriteTools), nameof(RoutineWriteTools.UpdateRoutine), await RoutineWriteTools.UpdateRoutine(services, "routine-1", FixtureFactory.Create<UpdateRoutineCommand>(), null, true, true, default)),
      (typeof(RoutineWriteTools), nameof(RoutineWriteTools.CreateRoutineFolder), await RoutineWriteTools.CreateRoutineFolder(services, FixtureFactory.Create<CreateRoutineFolderCommand>(), true, default)),
      (typeof(ExerciseReadTools), nameof(ExerciseReadTools.GetExerciseTemplates), await ExerciseReadTools.GetExerciseTemplates(services, 1, 10, "full", default)),
      (typeof(ExerciseReadTools), nameof(ExerciseReadTools.GetExerciseTemplate), await ExerciseReadTools.GetExerciseTemplate(services, "template-1", default)),
      (typeof(ExerciseReadTools), nameof(ExerciseReadTools.GetExerciseHistory), await ExerciseReadTools.GetExerciseHistory(services, "template-1", 1, 10, start, end, "full", default)),
      (typeof(ExerciseWriteTools), nameof(ExerciseWriteTools.CreateExerciseTemplate), await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.Create<CreateExerciseTemplateCommand>(), true, default)),
      (typeof(MeasurementReadTools), nameof(MeasurementReadTools.GetBodyMeasurements), await MeasurementReadTools.GetBodyMeasurements(services, 1, 10, "full", default)),
      (typeof(MeasurementReadTools), nameof(MeasurementReadTools.GetBodyMeasurement), await MeasurementReadTools.GetBodyMeasurement(services, end, default)),
      (typeof(MeasurementWriteTools), nameof(MeasurementWriteTools.CreateBodyMeasurement), await MeasurementWriteTools.CreateBodyMeasurement(services, FixtureFactory.Create<CreateBodyMeasurementCommand>(), true, default)),
      (typeof(MeasurementWriteTools), nameof(MeasurementWriteTools.UpdateBodyMeasurement), await MeasurementWriteTools.UpdateBodyMeasurement(services, end, FixtureFactory.Create<UpdateBodyMeasurementCommand>(), null, true, true, default)),
      (typeof(UserTools), nameof(UserTools.GetUserInfo), await UserTools.GetUserInfo(services, default)),
      (typeof(WorkoutReadTools), nameof(WorkoutReadTools.GetWorkouts), await WorkoutReadTools.GetWorkouts(services, 1, 10, "compact", default)),
      (typeof(WorkoutReadTools), nameof(WorkoutReadTools.GetWorkoutEvents), await WorkoutReadTools.GetWorkoutEvents(services, 1, 10, since, "compact", default)),
      (typeof(RoutineReadTools), nameof(RoutineReadTools.GetRoutines), await RoutineReadTools.GetRoutines(services, 1, 10, "compact", default)),
      (typeof(ExerciseReadTools), nameof(ExerciseReadTools.GetExerciseHistory), await ExerciseReadTools.GetExerciseHistory(services, "template-1", 1, 10, start, end, "compact", default)),
      (typeof(WorkoutWriteTools), nameof(WorkoutWriteTools.CreateWorkout), await WorkoutWriteTools.CreateWorkout(services, FixtureFactory.Create<CreateWorkoutCommand>(), false, default)),
      (typeof(WorkoutWriteTools), nameof(WorkoutWriteTools.UpdateWorkout), await WorkoutWriteTools.UpdateWorkout(services, "workout-1", FixtureFactory.Create<UpdateWorkoutCommand>(), null, true, false, default)),
      (typeof(RoutineWriteTools), nameof(RoutineWriteTools.CreateRoutine), await RoutineWriteTools.CreateRoutine(services, FixtureFactory.Create<CreateRoutineCommand>(), false, default)),
      (typeof(RoutineWriteTools), nameof(RoutineWriteTools.UpdateRoutine), await RoutineWriteTools.UpdateRoutine(services, "routine-1", FixtureFactory.Create<UpdateRoutineCommand>(), null, true, false, default)),
      (typeof(RoutineWriteTools), nameof(RoutineWriteTools.CreateRoutineFolder), await RoutineWriteTools.CreateRoutineFolder(services, FixtureFactory.Create<CreateRoutineFolderCommand>(), false, default)),
      (typeof(ExerciseWriteTools), nameof(ExerciseWriteTools.CreateExerciseTemplate), await ExerciseWriteTools.CreateExerciseTemplate(services, FixtureFactory.Create<CreateExerciseTemplateCommand>(), false, default)),
      (typeof(MeasurementWriteTools), nameof(MeasurementWriteTools.CreateBodyMeasurement), await MeasurementWriteTools.CreateBodyMeasurement(services, FixtureFactory.Create<CreateBodyMeasurementCommand>(), false, default)),
      (typeof(MeasurementWriteTools), nameof(MeasurementWriteTools.UpdateBodyMeasurement), await MeasurementWriteTools.UpdateBodyMeasurement(services, end, FixtureFactory.Create<UpdateBodyMeasurementCommand>(), null, true, false, default)),
      (typeof(MeasurementWriteTools), nameof(MeasurementWriteTools.UpdateBodyMeasurement), await MeasurementWriteTools.UpdateBodyMeasurement(services, end, FixtureFactory.Create<UpdateBodyMeasurementCommand>(), since, false, false, default)),
      (typeof(CompositeTools), nameof(CompositeTools.SearchRoutines), await CompositeTools.SearchRoutines(services, "leg", 100, null, default)),
      (typeof(CompositeTools), nameof(CompositeTools.SearchExerciseTemplates), await CompositeTools.SearchExerciseTemplates(services, "squat", "barbell", "quadriceps", 100, null, default)),
      (typeof(CompositeTools), nameof(CompositeTools.GetWorkoutEvidence), await CompositeTools.GetWorkoutEvidence(services, 4, DateTimeOffset.Parse("2026-07-27T00:00:00Z"), 100, null, default)),
      (typeof(CompositeTools), nameof(CompositeTools.SummarizeTraining), await CompositeTools.SummarizeTraining(services, 4, DateTimeOffset.Parse("2026-07-27T00:00:00Z"), 100, null, default)),
      (typeof(CompositeTools), nameof(CompositeTools.SummarizeExerciseHistory), await CompositeTools.SummarizeExerciseHistory(services, "template-1", 4, DateTimeOffset.Parse("2026-07-27T00:00:00Z"), 100, null, default)),
      (typeof(DiagnosticTools), nameof(DiagnosticTools.GetDiagnostics), DiagnosticTools.GetDiagnostics(services)),
    };

    foreach (var testCase in cases)
    {
      var method = testCase.Type.GetMethod(testCase.Method, BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException(testCase.Method);
      var tool = McpServerTool.Create(method, target: null, new McpServerToolCreateOptions { SerializerOptions = ToolResults.JsonOptions });
      AssertMatches(ToolSchemas.NormalizeWireValues(tool.ProtocolTool.OutputSchema!.Value), testCase.Result.Structured(), testCase.Method);
    }
  }

  private static FakeHevyClient CompleteClient()
  {
    var workout = FakeHevyClient.SampleWorkout();
    var routine = FakeHevyClient.SampleRoutine();
    var client = new FakeHevyClient
    {
      WorkoutCount = 1,
      Workouts = new(1, 1, [workout]),
      WorkoutEvents = new(1, 1, [new UpdatedWorkoutEvent(workout), new DeletedWorkoutEvent("deleted-1", workout.UpdatedAt)]),
      Routines = new(1, 1, [routine]),
      RoutineFolders = new(1, 1, [new RoutineFolder(1, 0, "Legs", workout.UpdatedAt, workout.CreatedAt)]),
      ExerciseTemplates = new(1, 1, [new ExerciseTemplate("template-1", "Squat", "weight_reps", "quadriceps", ["glutes"], EquipmentCategory.Barbell, false)]),
      ExerciseHistory = [new ExerciseHistoryEntry("workout-1", "Leg Day", workout.StartTime, workout.EndTime, "template-1", 100, 5, null, null, 8, null, "normal")],
      BodyMeasurements = new(1, 1, [FakeHevyClient.SampleMeasurement()]),
    };
    return client;
  }

  private static void AssertMatches(JsonElement schema, JsonElement instance, string path)
  {
    if (schema.ValueKind is JsonValueKind.True) return;
    (schema.ValueKind).Should().Be(JsonValueKind.Object);

    if (schema.TryGetProperty("anyOf", out var anyOf))
    {
      if (anyOf.EnumerateArray().Any(candidate => Matches(candidate, instance))) return;
      false.Should().BeTrue($"{path} does not match any advertised schema branch: {instance.GetRawText()}");
    }

    if (schema.TryGetProperty("enum", out var enumValues))
    {
      (enumValues.EnumerateArray()).Should().Contain((expected => JsonElement.DeepEquals(expected, instance)));
    }

    if (schema.TryGetProperty("type", out var type))
    {
      var allowed = type.ValueKind == JsonValueKind.Array
          ? type.EnumerateArray().Select(static item => item.GetString()).ToArray()
          : [type.GetString()];
      var actualType = TypeName(instance);
      (allowed.Contains(actualType, StringComparer.Ordinal) || (actualType == "integer" && allowed.Contains("number", StringComparer.Ordinal))).Should().BeTrue($"{path} is {actualType}; advertised types are {string.Join(", ", allowed)}.");
    }

    if (instance.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var properties))
    {
      if (schema.TryGetProperty("required", out var required))
      {
        foreach (var name in required.EnumerateArray().Select(static item => item.GetString()!))
        {
          (instance.TryGetProperty(name, out _)).Should().BeTrue($"{path} omits required {name}.");
        }
      }

      foreach (var property in instance.EnumerateObject())
      {
        if (properties.TryGetProperty(property.Name, out var propertySchema))
        {
          AssertMatches(propertySchema, property.Value, $"{path}.{property.Name}");
        }
      }

      return;
    }

    if (instance.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
    {
      var index = 0;
      foreach (var item in instance.EnumerateArray()) AssertMatches(items, item, $"{path}[{index++}]");
    }
  }

  private static bool Matches(JsonElement schema, JsonElement instance)
  {
    try
    {
      AssertMatches(schema, instance, "$candidate");
      return true;
    }
    catch (Xunit.Sdk.XunitException)
    {
      return false;
    }
  }

  private static string TypeName(JsonElement instance) => instance.ValueKind switch
  {
    JsonValueKind.Object => "object",
    JsonValueKind.Array => "array",
    JsonValueKind.String => "string",
    JsonValueKind.Number when instance.TryGetInt64(out _) => "integer",
    JsonValueKind.Number => "number",
    JsonValueKind.True or JsonValueKind.False => "boolean",
    JsonValueKind.Null => "null",
    _ => throw new InvalidOperationException(instance.ValueKind.ToString()),
  };
}
