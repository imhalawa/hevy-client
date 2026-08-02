using System.Diagnostics;
using System.Text.Json;
using Hevy.Mcp.Configuration;

namespace TestSupport;

public static class McpStdioProcess
{
  public static Process Start(string apiKey, bool? readOnly = null)
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
    start.Environment["HEVY_API_KEY"] = apiKey;
    start.Environment["HEVY_MCP_TRANSPORT"] = "stdio";
    if (readOnly is not null) start.Environment["HEVY_READ_ONLY"] = readOnly.Value ? "true" : "false";
    else start.Environment.Remove("HEVY_READ_ONLY");
    start.Environment.Remove("HEVY_LOG_LEVEL");
    start.Environment.Remove("MCP_AUTH_TOKEN");
    return Process.Start(start) ?? throw new InvalidOperationException("Failed to start Hevy.Mcp.");
  }

  public static async Task InitializeAsync(Process process, string clientName = "test")
  {
    var request = JsonSerializer.Serialize(new
    {
      jsonrpc = "2.0",
      id = 1,
      method = "initialize",
      @params = new
      {
        protocolVersion = "2025-11-25",
        capabilities = new { },
        clientInfo = new { name = clientName, version = "1.0" },
      },
    });
    await SendAsync(process, request);
    using var response = await ReadAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");
  }

  public static async Task SendAsync(Process process, string message)
  {
    await process.StandardInput.WriteLineAsync(message);
    await process.StandardInput.FlushAsync();
  }

  public static async Task<JsonDocument> ReadAsync(Process process)
  {
    var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
    if (string.IsNullOrWhiteSpace(line)) throw new InvalidOperationException("The MCP server returned no protocol message.");
    return JsonDocument.Parse(line);
  }
}
