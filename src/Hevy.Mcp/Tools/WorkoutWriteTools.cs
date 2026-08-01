using System.ComponentModel;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class WorkoutWriteTools
{
  [McpServerTool(Name = "create_workout", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<CreateWorkoutRequest, Workout>, MutationMeta>))]
  [Description("Create a workout. Weights are kilograms, distance meters, duration seconds, and times include UTC offsets.")]
  internal static Task<CallToolResult> CreateWorkout(
      IServiceProvider services,
      CreateWorkoutRequest request,
      [Description("Validate and return the exact normalized payload without contacting Hevy.")] bool dry_run = false,
      CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
      {
        ArgumentNullException.ThrowIfNull(request);
        CreateWorkoutCommand command = request;
        var result = await command.ExecuteAsync(ToolResults.Client(services), dry_run, cancellationToken);
        if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateWorkoutRequest, Workout>(request), "Workout payload is valid; no request was sent.", ToolResults.DryRunMeta());
        ArgumentNullException.ThrowIfNull(result);
        return ToolResults.Success(ToolResults.MutationResult<CreateWorkoutRequest, Workout>(result), $"Created workout {result.Id}.", new MutationMeta(false));
      });

  [McpServerTool(Name = "update_workout", Destructive = true, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<UpdateWorkoutRequest, Workout>, MutationMeta>))]
  [Description("Replace a workout after an updated_at guard, or explicitly bypass the guard with force.")]
  internal static Task<CallToolResult> UpdateWorkout(
      IServiceProvider services,
      string workout_id,
      UpdateWorkoutRequest request,
      DateTimeOffset? expected_updated_at = null,
      [Description("Explicitly bypass the updated_at guard.")] bool force = false,
      [Description("Validate and return the exact normalized payload without contacting Hevy.")] bool dry_run = false,
      CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
      {
        ArgumentNullException.ThrowIfNull(request);
        UpdateWorkoutCommand command = request;
        var result = await command.ExecuteAsync(ToolResults.Client(services), workout_id, expected_updated_at, force, dry_run, cancellationToken);
        if (dry_run) return ToolResults.Success(ToolResults.DryRunData<UpdateWorkoutRequest, Workout>(request), "Workout replacement payload is valid; no request was sent.", ToolResults.DryRunMeta(force, expected_updated_at));
        ArgumentNullException.ThrowIfNull(result);
        return ToolResults.Success(ToolResults.MutationResult<UpdateWorkoutRequest, Workout>(result), $"Updated workout {result.Id}.", new MutationMeta(false, force, expected_updated_at));
      });
}
