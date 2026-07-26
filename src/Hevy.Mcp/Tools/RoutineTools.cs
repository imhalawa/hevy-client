using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class RoutineReadTools
{
  [McpServerTool(Name = "get_routines", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one page of routines; compact output omits nested exercises unless detail is full.")]
  internal static Task<CallToolResult> GetRoutines(IServiceProvider services, [Range(1, int.MaxValue)] int page = 1, [Range(1, 10)] int page_size = 10, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidatePagination(page, page_size);
    ToolResults.ValidateDetail(detail);
    var result = await ToolResults.Client(services).GetRoutinesAsync(page, page_size, cancellationToken);
    object items = detail == "full" ? result.Items : result.Items.Select(static routine => new { routine.Id, routine.Title, routine.FolderId, routine.UpdatedAt, routine.CreatedAt }).ToArray();
    return ToolResults.Success(new { items }, $"Returned {result.Items.Count} routines.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
  });

  [McpServerTool(Name = "get_routine", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one routine with complete nested exercises and sets; set units are kilograms, meters, and seconds.")]
  internal static Task<CallToolResult> GetRoutine(IServiceProvider services, string routine_id, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidateIdentifier(routine_id, nameof(routine_id));
    var item = await ToolResults.Client(services).GetRoutineAsync(routine_id, cancellationToken);
    return ToolResults.Success(item, $"Returned routine {item.Id}.");
  });

  [McpServerTool(Name = "get_routine_folders", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one page of routine folders with UTC updated and created timestamps.")]
  internal static Task<CallToolResult> GetRoutineFolders(IServiceProvider services, [Range(1, int.MaxValue)] int page = 1, [Range(1, 10)] int page_size = 10, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidatePagination(page, page_size);
    ToolResults.ValidateDetail(detail);
    var result = await ToolResults.Client(services).GetRoutineFoldersAsync(page, page_size, cancellationToken);
    return ToolResults.Success(new { items = result.Items }, $"Returned {result.Items.Count} routine folders.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
  });

  [McpServerTool(Name = "get_routine_folder", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one routine folder by numeric identifier.")]
  internal static Task<CallToolResult> GetRoutineFolder(IServiceProvider services, long folder_id, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(folder_id, 1);
    var item = await ToolResults.Client(services).GetRoutineFolderAsync(folder_id, cancellationToken);
    return ToolResults.Success(item, $"Returned routine folder {item.Id}.");
  });
}

internal static class RoutineWriteTools
{
  [McpServerTool(Name = "create_routine", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Create a routine with nested exercises and sets; set units are kilograms, meters, and seconds.")]
  internal static Task<CallToolResult> CreateRoutine(IServiceProvider services, CreateRoutineRequest request, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    ToolValidation.Routine(request.Routine);
    if (dry_run) return ToolResults.Success(request, "Routine payload is valid; no request was sent.", new { dry_run = true });
    var result = await ToolResults.Client(services).CreateRoutineAsync(request, cancellationToken);
    return ToolResults.Success(result, $"Created routine {result.Id}.", new { dry_run = false });
  });

  [McpServerTool(Name = "update_routine", Destructive = true, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Replace a routine after an updated_at guard, or explicitly bypass the guard with force.")]
  internal static Task<CallToolResult> UpdateRoutine(IServiceProvider services, string routine_id, UpdateRoutineRequest request, DateTimeOffset? expected_updated_at = null, bool force = false, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidateIdentifier(routine_id, nameof(routine_id));
    ArgumentNullException.ThrowIfNull(request);
    ToolValidation.Routine(request.Routine);
    ToolValidation.Guard(expected_updated_at, force);
    if (dry_run) return ToolResults.Success(request, "Routine replacement payload is valid; no request was sent.", new { dry_run = true, forced = force, expected_updated_at });
    var client = ToolResults.Client(services);
    if (!force)
    {
      var current = await client.GetRoutineAsync(routine_id, cancellationToken);
      if (current.UpdatedAt != expected_updated_at) return ToolExceptionFilter.Conflict("The routine changed since expected_updated_at; read it again before replacing it.");
    }
    var result = await client.UpdateRoutineAsync(routine_id, request, cancellationToken);
    return ToolResults.Success(result, $"Updated routine {result.Id}.", new { dry_run = false, forced = force, expected_updated_at });
  });

  [McpServerTool(Name = "create_routine_folder", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Create a routine folder at index zero; existing folder positions change.")]
  internal static Task<CallToolResult> CreateRoutineFolder(IServiceProvider services, CreateRoutineFolderRequest request, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(request.RoutineFolder);
    ToolValidation.Required(request.RoutineFolder.Title, "routine folder title");
    if (dry_run) return ToolResults.Success(request, "Routine-folder payload is valid; no request was sent.", new { dry_run = true });
    var result = await ToolResults.Client(services).CreateRoutineFolderAsync(request, cancellationToken);
    return ToolResults.Success(result, $"Created routine folder {result.Id}.", new { dry_run = false });
  });
}
