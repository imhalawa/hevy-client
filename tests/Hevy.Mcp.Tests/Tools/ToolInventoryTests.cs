using System.Text.Json;
using static TestSupport.McpStdioProcess;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class ToolInventoryTests
{
  private static readonly string[] CompositeNames =
  [
    "get_workout_evidence",
    "search_exercise_templates",
    "search_routines",
    "summarize_exercise_history",
    "summarize_training",
  ];

  private const string DiagnosticName = "get_diagnostics";

  private static readonly IReadOnlyDictionary<(string Method, string Path), string> ExpectedNames =
      new Dictionary<(string, string), string>
      {
        [("get", "/v1/workouts")] = "get_workouts",
        [("post", "/v1/workouts")] = "create_workout",
        [("get", "/v1/workouts/count")] = "get_workout_count",
        [("get", "/v1/workouts/events")] = "get_workout_events",
        [("get", "/v1/workouts/{workoutId}")] = "get_workout",
        [("put", "/v1/workouts/{workoutId}")] = "update_workout",
        [("get", "/v1/user/info")] = "get_user_info",
        [("get", "/v1/routines")] = "get_routines",
        [("post", "/v1/routines")] = "create_routine",
        [("get", "/v1/routines/{routineId}")] = "get_routine",
        [("put", "/v1/routines/{routineId}")] = "update_routine",
        [("get", "/v1/exercise_templates")] = "get_exercise_templates",
        [("post", "/v1/exercise_templates")] = "create_exercise_template",
        [("get", "/v1/exercise_templates/{exerciseTemplateId}")] = "get_exercise_template",
        [("get", "/v1/routine_folders")] = "get_routine_folders",
        [("post", "/v1/routine_folders")] = "create_routine_folder",
        [("get", "/v1/routine_folders/{folderId}")] = "get_routine_folder",
        [("get", "/v1/exercise_history/{exerciseTemplateId}")] = "get_exercise_history",
        [("get", "/v1/body_measurements")] = "get_body_measurements",
        [("post", "/v1/body_measurements")] = "create_body_measurement",
        [("get", "/v1/body_measurements/{date}")] = "get_body_measurement",
        [("put", "/v1/body_measurements/{date}")] = "update_body_measurement",
      };

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ToolsListMatchesEveryOfficialOperationAndReadOnlyOmitsWrites(bool readOnly)
  {
    var snapshotOperations = ReadSnapshotOperations();
    (snapshotOperations.Order()).Should().Equal(ExpectedNames.Keys.Order());

    using var process = Start("inventory-test-api-key", readOnly);
    await SendAsync(process, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"inventory-test","version":"1.0"}}}""");
    using var initialize = await ReadAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");
    await SendAsync(process, """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
    using var listed = await ReadAsync(process);
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    (listed.RootElement.TryGetProperty("result", out var result)).Should().BeTrue(listed.RootElement.GetRawText());
    var tools = result.GetProperty("tools").EnumerateArray().ToArray();
    var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToArray();
    var expected = ExpectedNames
        .Where(pair => !readOnly || pair.Key.Method == "get")
        .Select(pair => pair.Value)
        .Concat(CompositeNames)
        .Append(DiagnosticName)
        .Order().ToArray();

    (names.Order()).Should().Equal(expected);
    (names.Distinct(StringComparer.Ordinal).Count()).Should().Be(names.Length);
    foreach (var tool in tools)
    {
      var name = tool.GetProperty("name").GetString()!;
      var input = tool.GetProperty("inputSchema");
      var annotations = tool.GetProperty("annotations");
      (input.GetProperty("type").GetString()).Should().Be("object");
      (tool.GetProperty("outputSchema").GetProperty("type").GetString()).Should().Be("object");
      var outputData = tool.GetProperty("outputSchema").GetProperty("properties").GetProperty("data");
      (outputData.TryGetProperty("properties", out var outputDataProperties) && outputDataProperties.EnumerateObject().Any()).Should().BeTrue($"{name} data schema is not operation-specific.");
      (input.GetProperty("properties").TryGetProperty("services", out _)).Should().BeFalse();
      (input.GetProperty("properties").TryGetProperty("cancellation_token", out _)).Should().BeFalse();
      (Hint(annotations, "openWorldHint", defaultValue: true)).Should().Be(name != DiagnosticName);

      var isRead = name.StartsWith("get_", StringComparison.Ordinal) || CompositeNames.Contains(name, StringComparer.Ordinal);
      (Hint(annotations, "readOnlyHint", defaultValue: false)).Should().Be(isRead);
      if (isRead) continue;

      (input.GetProperty("properties").GetProperty("dry_run").GetProperty("default").GetBoolean()).Should().BeFalse();
      var isCreate = name.StartsWith("create_", StringComparison.Ordinal);
      (Hint(annotations, "destructiveHint", defaultValue: true)).Should().Be(!isCreate);
      (Hint(annotations, "idempotentHint", defaultValue: false)).Should().Be(!isCreate && name == "update_body_measurement");
      if (isCreate) continue;

      (input.GetProperty("properties").TryGetProperty("expected_updated_at", out _)).Should().BeTrue();
      (input.GetProperty("properties").GetProperty("force").GetProperty("default").GetBoolean()).Should().BeFalse();
    }
    var workoutsSchema = tools.Single(tool => tool.GetProperty("name").GetString() == "get_workouts")
        .GetProperty("inputSchema").GetProperty("properties");
    (workoutsSchema.GetProperty("page").GetProperty("minimum").GetInt32()).Should().Be(1);
    (workoutsSchema.GetProperty("page_size").GetProperty("minimum").GetInt32()).Should().Be(1);
    (workoutsSchema.GetProperty("page_size").GetProperty("maximum").GetInt32()).Should().Be(10);
    (workoutsSchema.GetProperty("detail").GetProperty("pattern").GetString()).Should().Be("^(compact|full)$");
    var exerciseTemplatesSchema = tools.Single(tool => tool.GetProperty("name").GetString() == "get_exercise_templates")
        .GetProperty("inputSchema").GetProperty("properties");
    (exerciseTemplatesSchema.GetProperty("page_size").GetProperty("minimum").GetInt32()).Should().Be(1);
    (exerciseTemplatesSchema.GetProperty("page_size").GetProperty("maximum").GetInt32()).Should().Be(100);
    var historyItemSchema = tools.Single(tool => tool.GetProperty("name").GetString() == "get_exercise_history")
        .GetProperty("outputSchema").GetProperty("properties").GetProperty("data")
        .GetProperty("properties").GetProperty("items").GetProperty("items").GetProperty("properties");
    (historyItemSchema.GetProperty("reps").GetProperty("type").EnumerateArray().Select(static value => value.GetString())).Should().Equal(["integer", "null"]);
    (historyItemSchema.GetProperty("distance_meters").GetProperty("type").EnumerateArray().Select(static value => value.GetString())).Should().Equal(["integer", "null"]);
    (historyItemSchema.GetProperty("duration_seconds").GetProperty("type").EnumerateArray().Select(static value => value.GetString())).Should().Equal(["integer", "null"]);
    AssertOutputShape(tools, "get_workouts", "items", "page", "continuation");
    AssertOutputShape(tools, "get_workout_count", "workout_count");
    AssertOutputShape(tools, "get_workout", "id");
    AssertOutputShape(tools, "get_routines", "items", "page", "continuation");
    AssertOutputShape(tools, "get_exercise_history", "items", "page", "continuation");
    AssertOutputShape(tools, "get_body_measurement", "date");
    AssertOutputShape(tools, DiagnosticName, "server_version", "runtime_version", "transport", "read_only", "diagnostics_enabled", "health");
    if (!readOnly)
    {
      AssertOutputShape(tools, "create_workout", "payload", "result", "dry_run", "validation_warnings");
      AssertOutputShape(tools, "update_body_measurement", "payload", "result", "forced", "expected_updated_at", "guard_available", "guard_limitation");
      var measurementUpdateInput = tools.Single(tool => tool.GetProperty("name").GetString() == "update_body_measurement")
          .GetProperty("inputSchema").GetProperty("properties");
      (measurementUpdateInput.GetProperty("force").GetProperty("description").GetString()).Should().ContainEquivalentOf("required");
      (measurementUpdateInput.GetProperty("expected_updated_at").GetProperty("description").GetString()).Should().ContainEquivalentOf("do not expose updated_at");
    }
    (process.ExitCode).Should().Be(0);
    (await process.StandardError.ReadToEndAsync()).Should().Be(string.Empty);
  }

  private static bool Hint(JsonElement annotations, string name, bool defaultValue) =>
      annotations.TryGetProperty(name, out var hint) ? hint.GetBoolean() : defaultValue;

  private static void AssertOutputShape(JsonElement[] tools, string name, params string[] properties)
  {
    var output = tools.Single(tool => tool.GetProperty("name").GetString() == name).GetProperty("outputSchema").GetProperty("properties");
    var data = output.GetProperty("data").GetProperty("properties");
    var hasMetaProperties = output.GetProperty("meta").TryGetProperty("properties", out var meta);
    foreach (var property in properties)
    {
      (data.TryGetProperty(property, out _) || (hasMetaProperties && meta.TryGetProperty(property, out _))).Should().BeTrue($"{name} output schema omits {property}.");
    }
  }

  private static HashSet<(string Method, string Path)> ReadSnapshotOperations()
  {
    var snapshot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/api/hevy-openapi-2026-07-26.json"));
    using var document = JsonDocument.Parse(File.ReadAllText(snapshot));
    var operations = new HashSet<(string, string)>();
    foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
    {
      foreach (var operation in path.Value.EnumerateObject())
      {
        if (operation.Name is "get" or "post" or "put")
        {
          operations.Add((operation.Name, path.Name));
        }
      }
    }

    return operations;
  }

}
