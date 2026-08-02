using System.ComponentModel;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class RoutineWriteTools
{
  [McpServerTool(Name = "create_routine", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<CreateRoutineRequest, Routine>, MutationMeta>))]
  [Description("Create a routine with nested exercises and sets; set units are kilograms, meters, and seconds.")]
  internal static async Task<CallToolResult> CreateRoutine(IServiceProvider services, CreateRoutineCommand request, bool dry_run = false, CancellationToken cancellationToken = default)
  {
    var result = await new CreateRoutineUseCase(ToolResults.Client(services)).ExecuteAsync(request, dry_run, cancellationToken);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateRoutineRequest, Routine>((CreateRoutineRequest)request), "Routine payload is valid; no request was sent.", ToolResults.DryRunMeta());
    var routine = result ?? throw new InvalidOperationException("The create-routine use case returned no result.");
    return ToolResults.Success(ToolResults.MutationResult<CreateRoutineRequest, Routine>(routine), $"Created routine {routine.Id}.", new MutationMeta(false));
  }

  [McpServerTool(Name = "update_routine", Destructive = true, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<UpdateRoutineRequest, Routine>, MutationMeta>))]
  [Description("Replace a routine after an updated_at guard, or explicitly bypass the guard with force.")]
  internal static async Task<CallToolResult> UpdateRoutine(IServiceProvider services, string routine_id, UpdateRoutineCommand request, DateTimeOffset? expected_updated_at = null, bool force = false, bool dry_run = false, CancellationToken cancellationToken = default)
  {
    var result = await new UpdateRoutineUseCase(ToolResults.Client(services)).ExecuteAsync(routine_id, request, expected_updated_at, force, dry_run, cancellationToken);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<UpdateRoutineRequest, Routine>((UpdateRoutineRequest)request), "Routine replacement payload is valid; no request was sent.", ToolResults.DryRunMeta(force, expected_updated_at));
    var routine = result ?? throw new InvalidOperationException("The update-routine use case returned no result.");
    return ToolResults.Success(ToolResults.MutationResult<UpdateRoutineRequest, Routine>(routine), $"Updated routine {routine.Id}.", new MutationMeta(false, force, expected_updated_at));
  }

  [McpServerTool(Name = "create_routine_folder", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<CreateRoutineFolderRequest, RoutineFolder>, MutationMeta>))]
  [Description("Create a routine folder at index zero; existing folder positions change.")]
  internal static async Task<CallToolResult> CreateRoutineFolder(IServiceProvider services, CreateRoutineFolderCommand request, bool dry_run = false, CancellationToken cancellationToken = default)
  {
    var result = await new CreateRoutineFolderUseCase(ToolResults.Client(services)).ExecuteAsync(request, dry_run, cancellationToken);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateRoutineFolderRequest, RoutineFolder>((CreateRoutineFolderRequest)request), "Routine-folder payload is valid; no request was sent.", ToolResults.DryRunMeta());
    var folder = result ?? throw new InvalidOperationException("The create-routine-folder use case returned no result.");
    return ToolResults.Success(ToolResults.MutationResult<CreateRoutineFolderRequest, RoutineFolder>(folder), $"Created routine folder {folder.Id}.", new MutationMeta(false));
  }
}
