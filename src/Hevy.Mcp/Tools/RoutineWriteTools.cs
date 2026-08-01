using System.ComponentModel;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class RoutineWriteTools
{
  [McpServerTool(Name = "create_routine", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<CreateRoutineRequest, Routine>, MutationMeta>))]
  [Description("Create a routine with nested exercises and sets; set units are kilograms, meters, and seconds.")]
  internal static Task<CallToolResult> CreateRoutine(IServiceProvider services, CreateRoutineRequest request, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    CreateRoutineCommand command = request;
    if (!dry_run) ToolResults.Cache(services)?.InvalidateRoutines();
    var result = await command.ExecuteAsync(ToolResults.Client(services), dry_run, cancellationToken);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateRoutineRequest, Routine>(request), "Routine payload is valid; no request was sent.", ToolResults.DryRunMeta());
    ArgumentNullException.ThrowIfNull(result);
    return ToolResults.Success(ToolResults.MutationResult<CreateRoutineRequest, Routine>(result), $"Created routine {result.Id}.", new MutationMeta(false));
  });

  [McpServerTool(Name = "update_routine", Destructive = true, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<UpdateRoutineRequest, Routine>, MutationMeta>))]
  [Description("Replace a routine after an updated_at guard, or explicitly bypass the guard with force.")]
  internal static Task<CallToolResult> UpdateRoutine(IServiceProvider services, string routine_id, UpdateRoutineRequest request, DateTimeOffset? expected_updated_at = null, bool force = false, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    UpdateRoutineCommand command = request;
    if (!dry_run) ToolResults.Cache(services)?.InvalidateRoutines();
    var result = await command.ExecuteAsync(ToolResults.Client(services), routine_id, expected_updated_at, force, dry_run, cancellationToken);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<UpdateRoutineRequest, Routine>(request), "Routine replacement payload is valid; no request was sent.", ToolResults.DryRunMeta(force, expected_updated_at));
    ArgumentNullException.ThrowIfNull(result);
    return ToolResults.Success(ToolResults.MutationResult<UpdateRoutineRequest, Routine>(result), $"Updated routine {result.Id}.", new MutationMeta(false, force, expected_updated_at));
  });

  [McpServerTool(Name = "create_routine_folder", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<CreateRoutineFolderRequest, RoutineFolder>, MutationMeta>))]
  [Description("Create a routine folder at index zero; existing folder positions change.")]
  internal static Task<CallToolResult> CreateRoutineFolder(IServiceProvider services, CreateRoutineFolderRequest request, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    CreateRoutineFolderCommand command = request;
    var result = await command.ExecuteAsync(ToolResults.Client(services), dry_run, cancellationToken);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateRoutineFolderRequest, RoutineFolder>(request), "Routine-folder payload is valid; no request was sent.", ToolResults.DryRunMeta());
    ArgumentNullException.ThrowIfNull(result);
    return ToolResults.Success(ToolResults.MutationResult<CreateRoutineFolderRequest, RoutineFolder>(result), $"Created routine folder {result.Id}.", new MutationMeta(false));
  });
}
