using System.Text.Json;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

internal static class ToolTestJson
{
  internal static JsonElement Structured(this CallToolResult result)
  {
    (result.StructuredContent).Should().NotBeNull();
    return JsonSerializer.SerializeToElement(result.StructuredContent).Deserialize<JsonElement>();
  }
}
