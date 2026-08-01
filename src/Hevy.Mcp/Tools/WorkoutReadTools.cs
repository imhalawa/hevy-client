using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hevy.Core.Models;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class WorkoutReadTools
{
  [McpServerTool(Name = "get_workouts", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<ItemsData<WorkoutListItem>, PageMeta<PageContinuation>>))]
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

  [McpServerTool(Name = "get_workout_count", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<WorkoutCountData, NoMeta>))]
  [Description("Get the total workout count for the authenticated Hevy account.")]
  internal static Task<CallToolResult> GetWorkoutCount(IServiceProvider services, CancellationToken cancellationToken = default) =>
      ToolExceptionFilter.ExecuteAsync(async () =>
      {
        var count = await ToolResults.Client(services).GetWorkoutCountAsync(cancellationToken);
        return ToolResults.Success(new { workout_count = count }, $"The account has {count} workouts.");
      });

  [McpServerTool(Name = "get_workout_events", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<ItemsData<WorkoutEventListItem>, PageMeta<WorkoutEventContinuation>>))]
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
        var items = result.Items.Select(workoutEvent => ProjectEvent(workoutEvent, detail == "full")).ToArray();
        return ToolResults.Success(new { items }, $"Returned {result.Items.Count} workout events.", ToolResults.WorkoutEventPageMeta(result.Page, result.PageCount, page_size, since, detail));
      });

  [McpServerTool(Name = "get_workout", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<Workout, NoMeta>))]
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

  private static WorkoutEventListItem ProjectEvent(WorkoutEvent workoutEvent, bool full) => workoutEvent switch
  {
    UpdatedWorkoutEvent updated => new("updated", updated.Workout.Id, updated.Workout.UpdatedAt, Workout: full ? updated.Workout : null),
    DeletedWorkoutEvent deleted => new("deleted", deleted.Id, DeletedAt: deleted.DeletedAt),
    _ => new("unknown", string.Empty),
  };
}
