using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class WorkoutReadTools
{
  [McpServerTool(Name = "get_workouts", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one page of workouts. Times are UTC offsets; compact output omits nested exercises unless detail is full.")]
  internal static Task<CallToolResult> GetWorkouts(
      IServiceProvider services,
      [Description("Page number, starting at 1."), Range(1, int.MaxValue)] int page = 1,
      [Description("Items per page, from 1 through 10."), Range(1, 10)] int page_size = 10,
      [Description("compact omits exercises; full returns complete nested workouts."), RegularExpression("^(compact|full)$")] string detail = "compact",
      CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
      {
        ToolResults.ValidatePagination(page, page_size);
        ToolResults.ValidateDetail(detail);
        var result = await ToolResults.Client(services).GetWorkoutsAsync(page, page_size, cancellationToken);
        object items = detail == "full"
            ? result.Items
            : result.Items.Select(static workout => new
            {
              workout.Id,
              workout.Title,
              workout.RoutineId,
              workout.Description,
              workout.StartTime,
              workout.EndTime,
              workout.UpdatedAt,
              workout.CreatedAt,
            }).ToArray();
        return ToolResults.Success(new { items }, $"Returned {result.Items.Count} workouts.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
      });

  [McpServerTool(Name = "get_workout_count", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get the total workout count for the authenticated Hevy account.")]
  internal static Task<CallToolResult> GetWorkoutCount(IServiceProvider services, CancellationToken cancellationToken = default) =>
      ToolExceptionFilter.ExecuteAsync(async () =>
      {
        var count = await ToolResults.Client(services).GetWorkoutCountAsync(cancellationToken);
        return ToolResults.Success(new { workout_count = count }, $"The account has {count} workouts.");
      });

  [McpServerTool(Name = "get_workout_events", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one page of workout update/delete events since a UTC timestamp.")]
  internal static Task<CallToolResult> GetWorkoutEvents(
      IServiceProvider services,
      [Description("Page number, starting at 1."), Range(1, int.MaxValue)] int page,
      [Description("Items per page, from 1 through 10."), Range(1, 10)] int page_size,
      [Description("Return events at or after this timestamp, including a UTC offset.")] DateTimeOffset since,
      [Description("compact returns event summaries; full returns complete updated workouts."), RegularExpression("^(compact|full)$")] string detail = "compact",
      CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
      {
        ToolResults.ValidatePagination(page, page_size);
        ToolResults.ValidateDetail(detail);
        if (since == default) throw new ArgumentException("since is required.", nameof(since));
        var result = await ToolResults.Client(services).GetWorkoutEventsAsync(page, page_size, since, cancellationToken);
        object items = detail == "full" ? result.Items : result.Items.Select(CompactEvent).ToArray();
        return ToolResults.Success(new { items }, $"Returned {result.Items.Count} workout events.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
      });

  [McpServerTool(Name = "get_workout", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one workout with its complete nested exercise and set data.")]
  internal static Task<CallToolResult> GetWorkout(
      IServiceProvider services,
      [Description("Hevy workout identifier.")] string workout_id,
      CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
      {
        ToolResults.ValidateIdentifier(workout_id, nameof(workout_id));
        var workout = await ToolResults.Client(services).GetWorkoutAsync(workout_id, cancellationToken);
        return ToolResults.Success(workout, $"Returned workout {workout.Id}.");
      });

  private static object CompactEvent(WorkoutEvent workoutEvent) => workoutEvent switch
  {
    UpdatedWorkoutEvent updated => new { type = "updated", id = updated.Workout.Id, updated_at = updated.Workout.UpdatedAt },
    DeletedWorkoutEvent deleted => new { type = "deleted", id = deleted.Id, deleted_at = deleted.DeletedAt },
    _ => new { type = "unknown", id = string.Empty, updated_at = (DateTimeOffset?)null },
  };
}

internal static class WorkoutWriteTools
{
  [McpServerTool(Name = "create_workout", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Create a workout. Weights are kilograms, distance meters, duration seconds, and times include UTC offsets.")]
  internal static Task<CallToolResult> CreateWorkout(
      IServiceProvider services,
      CreateWorkoutRequest request,
      [Description("Validate and return the exact normalized payload without contacting Hevy.")] bool dry_run = false,
      CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
      {
        ArgumentNullException.ThrowIfNull(request);
        ToolValidation.Workout(request.Workout);
        if (dry_run) return ToolResults.Success(request, "Workout payload is valid; no request was sent.", new { dry_run = true });
        var result = await ToolResults.Client(services).CreateWorkoutAsync(request, cancellationToken);
        return ToolResults.Success(result, $"Created workout {result.Id}.", new { dry_run = false });
      });

  [McpServerTool(Name = "update_workout", Destructive = true, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
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
        ToolResults.ValidateIdentifier(workout_id, nameof(workout_id));
        ArgumentNullException.ThrowIfNull(request);
        ToolValidation.Workout(request.Workout);
        ToolValidation.Guard(expected_updated_at, force);
        if (dry_run) return ToolResults.Success(request, "Workout replacement payload is valid; no request was sent.", new { dry_run = true, forced = force, expected_updated_at });
        var client = ToolResults.Client(services);
        if (!force)
        {
          var current = await client.GetWorkoutAsync(workout_id, cancellationToken);
          if (current.UpdatedAt != expected_updated_at)
          {
            return ToolExceptionFilter.Conflict("The workout changed since expected_updated_at; read it again before replacing it.");
          }
        }
        var result = await client.UpdateWorkoutAsync(workout_id, request, cancellationToken);
        return ToolResults.Success(result, $"Updated workout {result.Id}.", new { dry_run = false, forced = force, expected_updated_at });
      });
}
