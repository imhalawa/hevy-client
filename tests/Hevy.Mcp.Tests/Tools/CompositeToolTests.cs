using System.Diagnostics;
using System.Text.Json;
using Hevy.Mcp.Configuration;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class CompositeToolTests
{
  private static readonly string[] ExpectedCompositeTools =
  [
    "get_workout_evidence",
    "search_exercise_templates",
    "search_routines",
    "summarize_exercise_history",
    "summarize_training",
  ];

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task RealInventoryAlwaysIncludesFiveReadOnlyCompositeTools(bool readOnly)
  {
    using var process = StartServer(readOnly);
    await InitializeAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
    using var response = await ReadAsync(process);
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    var tools = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
    var composites = tools.Where(tool => ExpectedCompositeTools.Contains(tool.GetProperty("name").GetString(), StringComparer.Ordinal)).ToArray();
    Assert.Equal(ExpectedCompositeTools, composites.Select(tool => tool.GetProperty("name").GetString()!).Order());
    Assert.All(composites, tool =>
    {
      Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
      Assert.Equal("object", tool.GetProperty("inputSchema").GetProperty("type").GetString());
      Assert.Equal("object", tool.GetProperty("outputSchema").GetProperty("type").GetString());
    });
    Assert.Equal(readOnly ? 19 : 27, tools.Length);
  }

  [Fact]
  public async Task RealCompositeCallValidatesBoundsBeforeAnyHevyRequest()
  {
    using var process = StartServer(false);
    await InitializeAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"summarize_training","arguments":{"weeks":53}}}""");
    using var response = await ReadAsync(process);
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    var result = response.RootElement.GetProperty("result");
    Assert.True(result.GetProperty("isError").GetBoolean());
    Assert.Equal("validation_error", result.GetProperty("structuredContent").GetProperty("error").GetProperty("code").GetString());
    Assert.Equal(string.Empty, await process.StandardError.ReadToEndAsync());
  }

  private static Process StartServer(bool readOnly)
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
    start.Environment["HEVY_API_KEY"] = "composite-contract-test";
    start.Environment["HEVY_READ_ONLY"] = readOnly.ToString().ToLowerInvariant();
    start.Environment.Remove("HEVY_MCP_TRANSPORT");
    start.Environment.Remove("MCP_AUTH_TOKEN");
    return Process.Start(start) ?? throw new InvalidOperationException("Failed to start Hevy.Mcp.");
  }

  private static async Task InitializeAsync(Process process)
  {
    await SendAsync(process, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"composite-test","version":"1.0"}}}""");
    using var initialize = await ReadAsync(process);
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
    Assert.False(string.IsNullOrWhiteSpace(line));
    return JsonDocument.Parse(line);
  }
}
