using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hevy.Mcp.Tools;

internal static class ToolSchemas
{
  internal static JsonElement NormalizeWireValues(JsonElement schema)
  {
    var root = JsonNode.Parse(schema.GetRawText()) ?? throw new InvalidOperationException("Tool schema is empty.");
    NormalizeNode(root);
    return JsonSerializer.SerializeToElement(root, ToolResults.JsonOptions);
  }

  private static void NormalizeNode(JsonNode node)
  {
    if (node is JsonObject schemaObject)
    {
      if (schemaObject["properties"] is JsonObject properties)
      {
        var hasUnconstrainedType = properties["type"] is JsonValue typeSchema &&
            typeSchema.TryGetValue<bool>(out var unconstrained) &&
            unconstrained;
        if (hasUnconstrainedType)
        {
          properties["type"] = new JsonObject
          {
            ["type"] = "string",
            ["enum"] = new JsonArray("warmup", "normal", "failure", "dropset"),
          };
        }

        if (properties.ContainsKey("rpe"))
        {
          properties["rpe"] = new JsonObject
          {
            ["type"] = new JsonArray("number", "null"),
            ["enum"] = new JsonArray(6, 7, 7.5m, 8, 8.5m, 9, 9.5m, 10, null),
          };
        }
      }

    }

    var children = node switch
    {
      JsonObject jsonObject => jsonObject.Select(static pair => pair.Value),
      JsonArray jsonArray => jsonArray,
      _ => [],
    };
    foreach (var child in children.Where(static child => child is not null)) NormalizeNode(child!);
  }
}
