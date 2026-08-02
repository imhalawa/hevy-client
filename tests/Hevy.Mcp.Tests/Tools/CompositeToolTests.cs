using System.Text.Json;
using static TestSupport.McpStdioProcess;
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
    using var process = Start("composite-contract-test", readOnly);
    await InitializeAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
    using var response = await ReadAsync(process);
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    var tools = response.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
    var composites = tools.Where(tool => ExpectedCompositeTools.Contains(tool.GetProperty("name").GetString(), StringComparer.Ordinal)).ToArray();
    (composites.Select(tool => tool.GetProperty("name").GetString()!).Order()).Should().Equal(ExpectedCompositeTools);
    (composites).Should().AllSatisfy(tool =>
    {
      (tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean()).Should().BeTrue();
      (tool.GetProperty("inputSchema").GetProperty("type").GetString()).Should().Be("object");
      (tool.GetProperty("outputSchema").GetProperty("type").GetString()).Should().Be("object");
    });
    (tools.Length).Should().Be(readOnly ? 20 : 28);
  }

  [Fact]
  public async Task RealCompositeCallValidatesBoundsBeforeAnyHevyRequest()
  {
    using var process = Start("composite-contract-test", false);
    await InitializeAsync(process);
    await SendAsync(process, """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"summarize_training","arguments":{"weeks":53}}}""");
    using var response = await ReadAsync(process);
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    var result = response.RootElement.GetProperty("result");
    (result.GetProperty("isError").GetBoolean()).Should().BeTrue();
    (result.GetProperty("structuredContent").GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (await process.StandardError.ReadToEndAsync()).Should().Be(string.Empty);
  }

  [Fact]
  public async Task RealCompositeProtocolRejectsExtremeHistoryContinuationAsValidationError()
  {
    var token = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new
    {
      endpoint = "exercise-history-summary",
      filters = new Dictionary<string, string?>
      {
        ["end_utc"] = "2026-07-27T00:00:00.0000000+00:00",
        ["exercise_template_id"] = "template-1",
        ["limit"] = "100",
        ["page_size"] = "100",
        ["phase"] = "history",
        ["start_utc"] = "2026-06-29T00:00:00.0000000+00:00",
        ["weeks"] = "4",
      },
      next_page = int.MaxValue,
      remaining_item_budget = 1_000,
    })).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    var request = JsonSerializer.Serialize(new
    {
      jsonrpc = "2.0",
      id = 4,
      method = "tools/call",
      @params = new
      {
        name = "summarize_exercise_history",
        arguments = new
        {
          exercise_template_id = "template-1",
          weeks = 4,
          range_end_utc = "2026-07-27T00:00:00Z",
          limit = 100,
          continuation = token,
        },
      },
    });
    using var process = Start("composite-contract-test", false);
    await InitializeAsync(process);
    await SendAsync(process, request);
    using var response = await ReadAsync(process);
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    var result = response.RootElement.GetProperty("result");
    (result.GetProperty("isError").GetBoolean()).Should().BeTrue();
    (result.GetProperty("structuredContent").GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (await process.StandardError.ReadToEndAsync()).Should().Be(string.Empty);
  }

}
