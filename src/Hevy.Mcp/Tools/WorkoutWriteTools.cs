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
        CreateWorkoutCommand command = request;
        var result = await new CreateWorkoutUseCase(ToolResults.Client(services)).ExecuteAsync(command, dry_run, cancellationToken);
        if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateWorkoutRequest, Workout>(request), "Workout payload is valid; no request was sent.", ToolResults.DryRunMeta());
        var workout = result ?? throw new InvalidOperationException("The create-workout use case returned no result.");
        return ToolResults.Success(ToolResults.MutationResult<CreateWorkoutRequest, Workout>(workout), $"Created workout {workout.Id}.", new MutationMeta(false));
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
        UpdateWorkoutCommand command = request;
        var result = await new UpdateWorkoutUseCase(ToolResults.Client(services)).ExecuteAsync(workout_id, command, expected_updated_at, force, dry_run, cancellationToken);
        if (dry_run) return ToolResults.Success(ToolResults.DryRunData<UpdateWorkoutRequest, Workout>(request), "Workout replacement payload is valid; no request was sent.", ToolResults.DryRunMeta(force, expected_updated_at));
        var workout = result ?? throw new InvalidOperationException("The update-workout use case returned no result.");
        return ToolResults.Success(ToolResults.MutationResult<UpdateWorkoutRequest, Workout>(workout), $"Updated workout {workout.Id}.", new MutationMeta(false, force, expected_updated_at));
      });
}
