using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hevy.Core.Models;
using Hevy.Client.Contracts;
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
    var command = request.ToCommand();
    ToolValidation.Routine(command.Routine);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateRoutineRequest, Routine>(request), "Routine payload is valid; no request was sent.", ToolResults.DryRunMeta());
    ToolResults.Cache(services)?.InvalidateRoutines();
    var result = await ToolResults.Client(services).CreateRoutineAsync(command, cancellationToken);
    return ToolResults.Success(ToolResults.MutationResult<CreateRoutineRequest, Routine>(result), $"Created routine {result.Id}.", new MutationMeta(false));
  });

  [McpServerTool(Name = "update_routine", Destructive = true, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<UpdateRoutineRequest, Routine>, MutationMeta>))]
  [Description("Replace a routine after an updated_at guard, or explicitly bypass the guard with force.")]
  internal static Task<CallToolResult> UpdateRoutine(IServiceProvider services, string routine_id, UpdateRoutineRequest request, DateTimeOffset? expected_updated_at = null, bool force = false, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidateIdentifier(routine_id, nameof(routine_id));
    ArgumentNullException.ThrowIfNull(request);
    var command = request.ToCommand();
    ToolValidation.Routine(command.Routine);
    ToolValidation.Guard(expected_updated_at, force);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<UpdateRoutineRequest, Routine>(request), "Routine replacement payload is valid; no request was sent.", ToolResults.DryRunMeta(force, expected_updated_at));
    var client = ToolResults.Client(services);
    if (!force)
    {
      var current = await client.GetRoutineAsync(routine_id, cancellationToken);
      if (current.UpdatedAt != expected_updated_at) return ToolExceptionFilter.Conflict("The routine changed since expected_updated_at; read it again before replacing it.");
    }
    ToolResults.Cache(services)?.InvalidateRoutines();
    var result = await client.UpdateRoutineAsync(routine_id, command, cancellationToken);
    return ToolResults.Success(ToolResults.MutationResult<UpdateRoutineRequest, Routine>(result), $"Updated routine {result.Id}.", new MutationMeta(false, force, expected_updated_at));
  });

  [McpServerTool(Name = "create_routine_folder", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<CreateRoutineFolderRequest, RoutineFolder>, MutationMeta>))]
  [Description("Create a routine folder at index zero; existing folder positions change.")]
  internal static Task<CallToolResult> CreateRoutineFolder(IServiceProvider services, CreateRoutineFolderRequest request, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(request.RoutineFolder);
    ToolValidation.Required(request.RoutineFolder.Title, "routine folder title");
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateRoutineFolderRequest, RoutineFolder>(request), "Routine-folder payload is valid; no request was sent.", ToolResults.DryRunMeta());
    var result = await ToolResults.Client(services).CreateRoutineFolderAsync(request.ToCommand(), cancellationToken);
    return ToolResults.Success(ToolResults.MutationResult<CreateRoutineFolderRequest, RoutineFolder>(result), $"Created routine folder {result.Id}.", new MutationMeta(false));
  });
}
