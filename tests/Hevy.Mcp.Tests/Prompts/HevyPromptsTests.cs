using System.Diagnostics;
using System.Text.Json;
using Hevy.Mcp.Configuration;
using Xunit;

namespace Hevy.Mcp.Tests.Prompts;

public sealed class HevyPromptsTests
{
  [Fact]
  public async Task RealPromptInventoryAndAnalysisPromptRequireEvidenceCitations()
  {
    using var process = StartServer();
    await InitializeAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","id":2,"method":"prompts/list","params":{}}""");
    using var listed = await ReadAsync(process);
    var names = listed.RootElement.GetProperty("result").GetProperty("prompts").EnumerateArray()
        .Select(prompt => prompt.GetProperty("name").GetString()!).Order().ToArray();
    (names).Should().Equal(["analyze_recent_training", "create_completed_workout_from_routine"]);

    await SendAsync(process, """{"jsonrpc":"2.0","id":3,"method":"prompts/get","params":{"name":"analyze_recent_training","arguments":{"weeks":"6"}}}""");
    using var result = await ReadAsync(process);
    var text = PromptText(result);
    (text).Should().Contain("summarize_training");
    (text).Should().ContainEquivalentOf("evidence");
    (text).Should().ContainEquivalentOf("cite");
    (text).Should().ContainEquivalentOf("identifier");
    (text).Should().ContainEquivalentOf("timestamp");
    (text).Should().ContainEquivalentOf("summing chunk frequency and volume");
    (text).Should().ContainEquivalentOf("earliest and latest");

    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    (await process.StandardError.ReadToEndAsync()).Should().Be(string.Empty);
  }

  [Fact]
  public async Task RoutinePromptRequiresActualCompletedSetsAndEndTimeBeforeMutation()
  {
    using var process = StartServer();
    await InitializeAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","id":4,"method":"prompts/get","params":{"name":"create_completed_workout_from_routine","arguments":{"routine_id":"routine-1"}}}""");
    using var result = await ReadAsync(process);
    var text = PromptText(result);

    (text).Should().Contain("get_routine");
    (text).Should().ContainEquivalentOf("actual completed-set results");
    (text).Should().ContainEquivalentOf("actual end time");
    (text).Should().ContainEquivalentOf("do not invent");
    (text).Should().Contain("create_workout");

    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
  }

  private static string PromptText(JsonDocument response) => response.RootElement.GetProperty("result").GetProperty("messages")[0]
      .GetProperty("content").GetProperty("text").GetString()!;

  private static Process StartServer()
  {
    var start = new ProcessStartInfo
    {
      FileName = "dotnet",
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
    };
    start.ArgumentList.Add(typeof(HevyMcpOptions).Assembly.Location);
    start.Environment["HEVY_API_KEY"] = "prompt-contract-test";
    start.Environment.Remove("HEVY_MCP_TRANSPORT");
    start.Environment.Remove("HEVY_READ_ONLY");
    start.Environment.Remove("HEVY_LOG_LEVEL");
    start.Environment.Remove("MCP_AUTH_TOKEN");
    return Process.Start(start) ?? throw new InvalidOperationException("Failed to start Hevy.Mcp.");
  }

  private static async Task InitializeAsync(Process process)
  {
    await SendAsync(process, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"prompt-test","version":"1.0"}}}""");
    using var initialized = await ReadAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");
  }

  private static async Task SendAsync(Process process, string message)
  {
    await process.StandardInput.WriteLineAsync(message);
    await process.StandardInput.FlushAsync();
  }

  private static async Task<JsonDocument> ReadAsync(Process process)
  {
    var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
    (string.IsNullOrWhiteSpace(line)).Should().BeFalse();
    return JsonDocument.Parse(line!);
  }
}
