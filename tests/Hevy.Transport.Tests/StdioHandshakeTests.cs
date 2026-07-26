using System.Diagnostics;
using System.Text.Json;
using Hevy.Mcp.Configuration;
using Xunit;

namespace Hevy.Transport.Tests;

public sealed class StdioHandshakeTests
{
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

    Assert.Equal(1, initializeResponse.RootElement.GetProperty("id").GetInt32());
    Assert.Equal("hevy-client", initializeResponse.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
    Assert.Equal(2, toolsResponse.RootElement.GetProperty("id").GetInt32());
    var tools = toolsResponse.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
    Assert.Equal(22, tools.Length);
    Assert.All(tools, tool =>
    {
      Assert.Equal("object", tool.GetProperty("inputSchema").GetProperty("type").GetString());
      Assert.Equal("object", tool.GetProperty("outputSchema").GetProperty("type").GetString());
    });
    var callResult = callResponse.RootElement.GetProperty("result");
    Assert.False(callResult.GetProperty("isError").GetBoolean());
    Assert.Equal("Transport Dry Run", callResult.GetProperty("structuredContent").GetProperty("data").GetProperty("routine_folder").GetProperty("title").GetString());
    Assert.True(callResult.GetProperty("structuredContent").GetProperty("meta").GetProperty("dry_run").GetBoolean());
    var invalidCallResult = invalidCallResponse.RootElement.GetProperty("result");
    Assert.True(invalidCallResult.GetProperty("isError").GetBoolean());
    Assert.Equal("validation_error", invalidCallResult.GetProperty("structuredContent").GetProperty("error").GetProperty("code").GetString());
    Assert.Equal(32, invalidCallResult.GetProperty("structuredContent").GetProperty("error").GetProperty("correlation_id").GetString()!.Length);
    Assert.Equal(0, process.ExitCode);
    Assert.Equal(string.Empty, await process.StandardError.ReadToEndAsync());
    Assert.Equal(string.Empty, await process.StandardOutput.ReadToEndAsync());
  }

  [Fact]
  public async Task MissingConfigurationUsesStderrAndA_NonzeroExitCode()
  {
    using var process = StartServer(apiKey: null);

    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    Assert.NotEqual(0, process.ExitCode);
    Assert.Contains("HEVY_API_KEY", await process.StandardError.ReadToEndAsync(), StringComparison.Ordinal);
    Assert.Equal(string.Empty, await process.StandardOutput.ReadToEndAsync());
  }

  private static Process StartServer(string? apiKey)
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
    if (apiKey is not null)
    {
      startInfo.Environment["HEVY_API_KEY"] = apiKey;
    }

    var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Hevy.Mcp.");
    return process;
  }

  private static async Task<JsonDocument> ReadProtocolMessageAsync(Process process, TimeSpan timeout)
  {
    var line = await process.StandardOutput.ReadLineAsync().WaitAsync(timeout);
    Assert.False(string.IsNullOrWhiteSpace(line));
    return JsonDocument.Parse(line);
  }
}
