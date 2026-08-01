using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class RoutineReadTools
{
  [McpServerTool(Name = "get_routines", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<ItemsData<RoutineListItem>, PageMeta<PageContinuation>>))]
  [Description("Get one page of routines; compact output omits nested exercises unless detail is full.")]
  internal static Task<CallToolResult> GetRoutines(IServiceProvider services, [Range(1, int.MaxValue)] int page = 1, [Range(1, 10)] int page_size = 10, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidatePagination(page, page_size);
    ToolResults.ValidateDetail(detail);
    var cache = ToolResults.Cache(services);
    var result = cache is null
        ? await ToolResults.Client(services).GetRoutinesAsync(page, page_size, cancellationToken)
        : ToolResults.LocalPage(await cache.GetRoutinesAsync(cancellationToken), page, page_size);
    object items = detail == "full" ? result.Items : result.Items.Select(static routine => new { routine.Id, routine.Title, routine.FolderId, routine.UpdatedAt, routine.CreatedAt }).ToArray();
    return ToolResults.Success(new { items }, $"Returned {result.Items.Count} routines.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
  });

  [McpServerTool(Name = "get_routine", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<Routine, NoMeta>))]
  [Description("Get one routine with complete nested exercises and sets; set units are kilograms, meters, and seconds.")]
  internal static Task<CallToolResult> GetRoutine(IServiceProvider services, string routine_id, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidateIdentifier(routine_id, nameof(routine_id));
    var client = ToolResults.Client(services);
    var cache = ToolResults.Cache(services);
    var item = cache is null
        ? await client.GetRoutineAsync(routine_id, cancellationToken)
        : (await cache.GetRoutinesAsync(cancellationToken)).SingleOrDefault(routine => string.Equals(routine.Id, routine_id, StringComparison.Ordinal))
            ?? await client.GetRoutineAsync(routine_id, cancellationToken);
    return ToolResults.Success(item, $"Returned routine {item.Id}.");
  });

  [McpServerTool(Name = "get_routine_folders", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<ItemsData<RoutineFolder>, PageMeta<PageContinuation>>))]
  [Description("Get one page of routine folders with UTC updated and created timestamps.")]
  internal static Task<CallToolResult> GetRoutineFolders(IServiceProvider services, [Range(1, int.MaxValue)] int page = 1, [Range(1, 10)] int page_size = 10, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidatePagination(page, page_size);
    ToolResults.ValidateDetail(detail);
    var result = await ToolResults.Client(services).GetRoutineFoldersAsync(page, page_size, cancellationToken);
    return ToolResults.Success(new { items = result.Items }, $"Returned {result.Items.Count} routine folders.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
  });

  [McpServerTool(Name = "get_routine_folder", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<RoutineFolder, NoMeta>))]
  [Description("Get one routine folder by numeric identifier.")]
  internal static Task<CallToolResult> GetRoutineFolder(IServiceProvider services, long folder_id, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(folder_id, 1);
    var item = await ToolResults.Client(services).GetRoutineFolderAsync(folder_id, cancellationToken);
    return ToolResults.Success(item, $"Returned routine folder {item.Id}.");
  });
}
