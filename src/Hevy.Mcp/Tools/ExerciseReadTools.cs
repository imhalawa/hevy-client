using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class ExerciseReadTools
{
  [McpServerTool(Name = "get_exercise_templates", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<ItemsData<ExerciseTemplate>, PageMeta<PageContinuation>>))]
  [Description("Get one page of exercise templates.")]
  internal static async Task<CallToolResult> GetExerciseTemplates(IServiceProvider services, [Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int page_size = 10, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default)
  {
    new PageRequest(page, page_size, 100, detail).Validate();
    var result = await ToolResults.Client(services).GetExerciseTemplatesAsync(page, page_size, cancellationToken);
    return ToolResults.Success(new { items = result.Items }, $"Returned {result.Items.Count} exercise templates.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
  }

  [McpServerTool(Name = "get_exercise_template", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<ExerciseTemplate, NoMeta>))]
  [Description("Get one complete exercise template by identifier.")]
  internal static async Task<CallToolResult> GetExerciseTemplate(IServiceProvider services, string exercise_template_id, CancellationToken cancellationToken = default)
  {
    var item = await ToolResults.Client(services).GetExerciseTemplateAsync(exercise_template_id, cancellationToken);
    return ToolResults.Success(item, $"Returned exercise template {item.Id}.");
  }

  [McpServerTool(Name = "get_exercise_history", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<ItemsData<ExerciseHistoryListItem>, ExerciseHistoryPageMeta>))]
  [Description("Get one bounded local window of exercise history between optional inclusive calendar dates; units are kilograms, meters, and seconds. One official unpaginated response is streamed with 1,000-item and 16 MiB safety limits.")]
  internal static async Task<CallToolResult> GetExerciseHistory(IServiceProvider services, string exercise_template_id, [Range(1, int.MaxValue)] int page = 1, [Range(1, 10)] int page_size = 10, DateOnly? start_date = null, DateOnly? end_date = null, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default)
  {
    new PageRequest(page, page_size, 10, detail).Validate();
    var result = await new GetExerciseHistoryUseCase(ToolResults.Client(services)).ExecuteAsync(exercise_template_id, page, page_size, start_date, end_date, cancellationToken);
    object items = detail == "full" ? result.Items : result.Items.Select(static entry => new { entry.WorkoutId, entry.WorkoutTitle, entry.WorkoutStartTime, entry.ExerciseTemplateId, entry.SetType }).ToArray();
    return ToolResults.Success(new { items }, $"Returned {result.Items.Count} exercise history entries.", ToolResults.ExerciseHistoryPageMeta(exercise_template_id, page, page_size, start_date, end_date, detail, result.ScannedItemCount, result.Truncated, result.TruncationReason));
  }
}
