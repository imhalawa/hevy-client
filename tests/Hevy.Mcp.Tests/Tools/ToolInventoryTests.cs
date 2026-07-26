using System.Diagnostics;
using System.Text.Json;
using Hevy.Mcp.Configuration;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class ToolInventoryTests
{
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
    Assert.Equal(ExpectedNames.Keys.Order(), snapshotOperations.Order());

    using var process = StartServer(readOnly);
    await SendAsync(process, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"inventory-test","version":"1.0"}}}""");
    using var initialize = await ReadAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");
    await SendAsync(process, """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
    using var listed = await ReadAsync(process);
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    Assert.True(listed.RootElement.TryGetProperty("result", out var result), listed.RootElement.GetRawText());
    var tools = result.GetProperty("tools").EnumerateArray().ToArray();
    var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToArray();
    var expected = ExpectedNames
        .Where(pair => !readOnly || pair.Key.Method == "get")
        .Select(pair => pair.Value).Order().ToArray();

    Assert.Equal(expected, names.Order());
    Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    foreach (var tool in tools)
    {
      var name = tool.GetProperty("name").GetString()!;
      var input = tool.GetProperty("inputSchema");
      var annotations = tool.GetProperty("annotations");
      Assert.Equal("object", input.GetProperty("type").GetString());
      Assert.Equal("object", tool.GetProperty("outputSchema").GetProperty("type").GetString());
      var outputData = tool.GetProperty("outputSchema").GetProperty("properties").GetProperty("data");
      Assert.True(outputData.TryGetProperty("properties", out var outputDataProperties) && outputDataProperties.EnumerateObject().Any(), $"{name} data schema is not operation-specific.");
      Assert.False(input.GetProperty("properties").TryGetProperty("services", out _));
      Assert.False(input.GetProperty("properties").TryGetProperty("cancellation_token", out _));
      Assert.True(Hint(annotations, "openWorldHint", defaultValue: true));

      if (name.StartsWith("get_", StringComparison.Ordinal))
      {
        Assert.True(Hint(annotations, "readOnlyHint", defaultValue: false));
      }
      else
      {
        Assert.False(Hint(annotations, "readOnlyHint", defaultValue: false));
        Assert.False(input.GetProperty("properties").GetProperty("dry_run").GetProperty("default").GetBoolean());
        if (name.StartsWith("create_", StringComparison.Ordinal))
        {
          Assert.False(Hint(annotations, "destructiveHint", defaultValue: true));
          Assert.False(Hint(annotations, "idempotentHint", defaultValue: false));
        }
        else
        {
          Assert.True(Hint(annotations, "destructiveHint", defaultValue: true));
          Assert.Equal(name == "update_body_measurement", Hint(annotations, "idempotentHint", defaultValue: false));
          Assert.True(input.GetProperty("properties").TryGetProperty("expected_updated_at", out _));
          Assert.False(input.GetProperty("properties").GetProperty("force").GetProperty("default").GetBoolean());
        }
      }
    }
    var workoutsSchema = tools.Single(tool => tool.GetProperty("name").GetString() == "get_workouts")
        .GetProperty("inputSchema").GetProperty("properties");
    Assert.Equal(1, workoutsSchema.GetProperty("page").GetProperty("minimum").GetInt32());
    Assert.Equal(1, workoutsSchema.GetProperty("page_size").GetProperty("minimum").GetInt32());
    Assert.Equal(10, workoutsSchema.GetProperty("page_size").GetProperty("maximum").GetInt32());
    Assert.Equal("^(compact|full)$", workoutsSchema.GetProperty("detail").GetProperty("pattern").GetString());
    AssertOutputShape(tools, "get_workouts", "items", "page", "continuation");
    AssertOutputShape(tools, "get_workout_count", "workout_count");
    AssertOutputShape(tools, "get_workout", "id");
    AssertOutputShape(tools, "get_routines", "items", "page", "continuation");
    AssertOutputShape(tools, "get_exercise_history", "items", "page", "continuation");
    AssertOutputShape(tools, "get_body_measurement", "date");
    if (!readOnly)
    {
      AssertOutputShape(tools, "create_workout", "payload", "result", "dry_run", "validation_warnings");
      AssertOutputShape(tools, "update_body_measurement", "payload", "result", "forced", "expected_updated_at", "guard_available", "guard_limitation");
      var measurementUpdateInput = tools.Single(tool => tool.GetProperty("name").GetString() == "update_body_measurement")
          .GetProperty("inputSchema").GetProperty("properties");
      Assert.Contains("required", measurementUpdateInput.GetProperty("force").GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
      Assert.Contains("do not expose updated_at", measurementUpdateInput.GetProperty("expected_updated_at").GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
    }
    Assert.Equal(0, process.ExitCode);
    Assert.Equal(string.Empty, await process.StandardError.ReadToEndAsync());
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
      Assert.True(data.TryGetProperty(property, out _) || (hasMetaProperties && meta.TryGetProperty(property, out _)), $"{name} output schema omits {property}.");
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

  private static Process StartServer(bool readOnly)
  {
    var startInfo = new ProcessStartInfo
    {
      FileName = "dotnet",
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
    };
    startInfo.ArgumentList.Add(typeof(HevyMcpOptions).Assembly.Location);
    startInfo.Environment["HEVY_API_KEY"] = "inventory-test-api-key";
    startInfo.Environment["HEVY_MCP_TRANSPORT"] = "stdio";
    startInfo.Environment["HEVY_READ_ONLY"] = readOnly ? "true" : "false";
    startInfo.Environment.Remove("MCP_AUTH_TOKEN");
    return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Hevy.Mcp.");
  }

  private static async Task SendAsync(Process process, string message)
  {
    await process.StandardInput.WriteLineAsync(message);
    await process.StandardInput.FlushAsync();
  }

  private static async Task<JsonDocument> ReadAsync(Process process)
  {
    var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
    Assert.False(string.IsNullOrWhiteSpace(line));
    return JsonDocument.Parse(line);
  }
}
