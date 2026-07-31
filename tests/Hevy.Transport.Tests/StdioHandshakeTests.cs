using System.Diagnostics;
using System.Text.Json;
using Hevy.Mcp.Configuration;
using Xunit;

namespace Hevy.Transport.Tests;

public sealed class StdioHandshakeTests
{
  private const string WorkoutDryRun = """{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"create_workout","arguments":{"request":{"workout":{"title":"Schema Workout","description":null,"start_time":"2026-07-25T10:00:00Z","end_time":"2026-07-25T11:00:00Z","is_private":false,"exercises":[{"exercise_template_id":"template-1","superset_id":null,"notes":null,"sets":[{"type":"normal","weight_kg":100,"reps":5,"distance_meters":null,"duration_seconds":null,"custom_metric":null,"rpe":8.5}]}]}},"dry_run":true}}}""";
  private const string CreateRoutineDryRun = """{"jsonrpc":"2.0","id":11,"method":"tools/call","params":{"name":"create_routine","arguments":{"request":{"routine":{"title":"Schema Routine","folder_id":null,"notes":"","exercises":[{"exercise_template_id":"template-1","superset_id":null,"rest_seconds":90,"notes":null,"sets":[{"type":"warmup","weight_kg":20,"reps":10,"distance_meters":null,"duration_seconds":null,"custom_metric":null,"rep_range":{"start":8,"end":12}}]}]}},"dry_run":true}}}""";
  private const string UpdateRoutineDryRun = """{"jsonrpc":"2.0","id":12,"method":"tools/call","params":{"name":"update_routine","arguments":{"routine_id":"routine-1","request":{"routine":{"title":"Schema Routine","notes":null,"exercises":[{"exercise_template_id":"template-1","superset_id":null,"rest_seconds":90,"notes":null,"sets":[{"type":"dropset","weight_kg":20,"reps":10,"distance_meters":null,"duration_seconds":null,"custom_metric":null,"rep_range":{"start":8,"end":12}}]}]}},"force":true,"dry_run":true}}}""";
  private const string ExerciseTemplateDryRun = """{"jsonrpc":"2.0","id":13,"method":"tools/call","params":{"name":"create_exercise_template","arguments":{"request":{"exercise":{"title":"Schema Press","exercise_type":"weight_reps","equipment_category":"barbell","muscle_group":"chest","other_muscles":["triceps","shoulders"]}},"dry_run":true}}}""";

  [Fact]
  public async Task BuiltExecutableCompletesInitializationAndListsToolsWithoutNonProtocolOutput()
  {
    using var process = StartServer("transport-test-api-key");

    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"transport-test","version":"1.0"}}}""");
    await process.StandardInput.FlushAsync();
    using var initializeResponse = await ReadProtocolMessageAsync(process, TimeSpan.FromSeconds(10));

    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
    await process.StandardInput.FlushAsync();

    using var toolsResponse = await ReadProtocolMessageAsync(process, TimeSpan.FromSeconds(10));

    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"create_routine_folder","arguments":{"request":{"routine_folder":{"title":"Transport Dry Run"}},"dry_run":true}}}""");
    await process.StandardInput.FlushAsync();
    using var callResponse = await ReadProtocolMessageAsync(process, TimeSpan.FromSeconds(10));

    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_workouts","arguments":{"page":"not-an-integer"}}}""");
    await process.StandardInput.FlushAsync();
    using var invalidCallResponse = await ReadProtocolMessageAsync(process, TimeSpan.FromSeconds(10));
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    (initializeResponse.RootElement.GetProperty("id").GetInt32()).Should().Be(1);
    (initializeResponse.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString()).Should().Be("hevy-client");
    (toolsResponse.RootElement.GetProperty("id").GetInt32()).Should().Be(2);
    var tools = toolsResponse.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
    (tools.Length).Should().Be(28);
    (tools).Should().AllSatisfy(tool =>
    {
      (tool.GetProperty("inputSchema").GetProperty("type").GetString()).Should().Be("object");
      (tool.GetProperty("outputSchema").GetProperty("type").GetString()).Should().Be("object");
    });
    var callResult = callResponse.RootElement.GetProperty("result");
    (callResult.GetProperty("isError").GetBoolean()).Should().BeFalse();
    (callResult.GetProperty("structuredContent").GetProperty("data").GetProperty("payload").GetProperty("routine_folder").GetProperty("title").GetString()).Should().Be("Transport Dry Run");
    (callResult.GetProperty("structuredContent").GetProperty("meta").GetProperty("dry_run").GetBoolean()).Should().BeTrue();
    var invalidCallResult = invalidCallResponse.RootElement.GetProperty("result");
    (invalidCallResult.GetProperty("isError").GetBoolean()).Should().BeTrue();
    (invalidCallResult.GetProperty("structuredContent").GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (invalidCallResult.GetProperty("structuredContent").GetProperty("error").GetProperty("correlation_id").GetString()!.Length).Should().Be(32);
    (process.ExitCode).Should().Be(0);
    (await process.StandardError.ReadToEndAsync()).Should().Be(string.Empty);
    (await process.StandardOutput.ReadToEndAsync()).Should().Be(string.Empty);
  }

  [Fact]
  public async Task MissingConfigurationUsesStderrAndA_NonzeroExitCode()
  {
    using var process = StartServer(apiKey: null);

    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    (process.ExitCode).Should().NotBe(0);
    (await process.StandardError.ReadToEndAsync()).Should().Contain("HEVY_API_KEY");
    (await process.StandardOutput.ReadToEndAsync()).Should().Be(string.Empty);
  }

  [Fact]
  public async Task MutationSchemasDescribeEveryCustomWireValueAcceptedByToolsCall()
  {
    using var process = StartServer("transport-test-api-key");
    await InitializeAsync(process);
    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
    await process.StandardInput.FlushAsync();
    using var listed = await ReadProtocolMessageAsync(process, TimeSpan.FromSeconds(10));
    var tools = listed.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();

    var workoutSet = FindTool(tools, "create_workout").GetProperty("inputSchema").GetProperty("properties")
        .GetProperty("request").GetProperty("properties").GetProperty("workout").GetProperty("properties")
        .GetProperty("exercises").GetProperty("items").GetProperty("properties").GetProperty("sets").GetProperty("items").GetProperty("properties");
    (Strings(workoutSet.GetProperty("type").GetProperty("enum"))).Should().Equal(["warmup", "normal", "failure", "dropset"]);
    (Strings(workoutSet.GetProperty("rpe").GetProperty("type"))).Should().Equal(["number", "null"]);
    (Literals(workoutSet.GetProperty("rpe").GetProperty("enum"))).Should().Equal(["6", "7", "7.5", "8", "8.5", "9", "9.5", "10", "null"]);

    var routineSet = FindTool(tools, "update_routine").GetProperty("inputSchema").GetProperty("properties")
        .GetProperty("request").GetProperty("properties").GetProperty("routine").GetProperty("properties")
        .GetProperty("exercises").GetProperty("items").GetProperty("properties").GetProperty("sets").GetProperty("items").GetProperty("properties");
    (Strings(routineSet.GetProperty("type").GetProperty("enum"))).Should().Equal(["warmup", "normal", "failure", "dropset"]);

    var exercise = FindTool(tools, "create_exercise_template").GetProperty("inputSchema").GetProperty("properties")
        .GetProperty("request").GetProperty("properties").GetProperty("exercise").GetProperty("properties");
    (Strings(exercise.GetProperty("exercise_type").GetProperty("enum"))).Should().Contain("weight_reps");
    (Strings(exercise.GetProperty("equipment_category").GetProperty("enum"))).Should().Contain("barbell");
    (Strings(exercise.GetProperty("muscle_group").GetProperty("enum"))).Should().Contain("chest");
    (Strings(exercise.GetProperty("other_muscles").GetProperty("items").GetProperty("enum"))).Should().Contain("triceps");

    foreach (var message in new[] { WorkoutDryRun, CreateRoutineDryRun, UpdateRoutineDryRun, ExerciseTemplateDryRun })
    {
      await process.StandardInput.WriteLineAsync(message);
      await process.StandardInput.FlushAsync();
      using var response = await ReadProtocolMessageAsync(process, TimeSpan.FromSeconds(10));
      (response.RootElement.GetProperty("result").GetProperty("isError").GetBoolean()).Should().BeFalse(response.RootElement.GetRawText());
    }

    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    (process.ExitCode).Should().Be(0);
    (await process.StandardError.ReadToEndAsync()).Should().Be(string.Empty);
  }

  [Fact]
  public async Task OptInDiagnosticsKeepStdoutProtocolOnlyAndWriteSafeRecordsToStderr()
  {
    using var process = StartServer("transport-fixture-secret-key", "Information");
    await InitializeAsync(process);
    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_diagnostics","arguments":{}}}""");
    await process.StandardInput.FlushAsync();
    using var response = await ReadProtocolMessageAsync(process, TimeSpan.FromSeconds(10));
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    var result = response.RootElement.GetProperty("result");
    (result.GetProperty("isError").GetBoolean()).Should().BeFalse(response.RootElement.GetRawText());
    var snapshot = result.GetProperty("structuredContent").GetProperty("data");
    (snapshot.GetProperty("transport").GetString()).Should().Be("stdio");
    (snapshot.GetProperty("diagnostics_enabled").GetBoolean()).Should().BeTrue();
    (response.RootElement.GetRawText()).Should().NotContain("transport-fixture-secret-key");
    (await process.StandardOutput.ReadToEndAsync()).Should().Be(string.Empty);

    var line = ((await process.StandardError.ReadToEndAsync()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)).Should().ContainSingle().Which;
    using var diagnostic = JsonDocument.Parse(line);
    (diagnostic.RootElement.GetProperty("operation_category").GetString()).Should().Be("diagnostics");
    (diagnostic.RootElement.GetProperty("status").GetString()).Should().Be("succeeded");
    (line).Should().NotContain("transport-fixture-secret-key");
  }

  private static Process StartServer(string? apiKey, string? logLevel = null)
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
    startInfo.Environment.Remove("HEVY_API_KEY");
    startInfo.Environment.Remove("HEVY_MCP_TRANSPORT");
    startInfo.Environment.Remove("HEVY_READ_ONLY");
    startInfo.Environment.Remove("MCP_AUTH_TOKEN");
    startInfo.Environment.Remove("HEVY_LOG_LEVEL");
    if (apiKey is not null)
    {
      startInfo.Environment["HEVY_API_KEY"] = apiKey;
    }
    if (logLevel is not null)
    {
      startInfo.Environment["HEVY_LOG_LEVEL"] = logLevel;
    }

    var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Hevy.Mcp.");
    return process;
  }

  private static async Task InitializeAsync(Process process)
  {
    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"schema-test","version":"1.0"}}}""");
    await process.StandardInput.FlushAsync();
    using var initialize = await ReadProtocolMessageAsync(process, TimeSpan.FromSeconds(10));
    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
  }

  private static JsonElement FindTool(IEnumerable<JsonElement> tools, string name) =>
      tools.Single(tool => tool.GetProperty("name").GetString() == name);

  private static string[] Strings(JsonElement values) => values.EnumerateArray().Select(value => value.GetString()!).ToArray();

  private static string[] Literals(JsonElement values) => values.EnumerateArray().Select(value => value.GetRawText().Trim('"')).ToArray();

  private static async Task<JsonDocument> ReadProtocolMessageAsync(Process process, TimeSpan timeout)
  {
    var line = await process.StandardOutput.ReadLineAsync().WaitAsync(timeout);
    (string.IsNullOrWhiteSpace(line)).Should().BeFalse();
    return JsonDocument.Parse(line!);
  }
}
