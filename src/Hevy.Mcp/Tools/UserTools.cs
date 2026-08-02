using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class UserTools
{
  [McpServerTool(Name = "get_user_info", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<Hevy.Core.Models.UserInfo, NoMeta>))]
  [Description("Get public account information for the authenticated Hevy user.")]
  internal static async Task<CallToolResult> GetUserInfo(IServiceProvider services, CancellationToken cancellationToken = default)
  {
    var item = await ToolResults.Client(services).GetUserInfoAsync(cancellationToken);
    return ToolResults.Success(item, $"Returned user {item.Id}.");
  }
}
