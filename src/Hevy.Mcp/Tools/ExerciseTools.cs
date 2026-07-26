using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class ExerciseReadTools
{
  [McpServerTool(Name = "get_exercise_templates", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one page of exercise templates.")]
  internal static Task<CallToolResult> GetExerciseTemplates(IServiceProvider services, [Range(1, int.MaxValue)] int page = 1, [Range(1, 10)] int page_size = 10, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidatePagination(page, page_size);
    ToolResults.ValidateDetail(detail);
    var result = await ToolResults.Client(services).GetExerciseTemplatesAsync(page, page_size, cancellationToken);
    return ToolResults.Success(new { items = result.Items }, $"Returned {result.Items.Count} exercise templates.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
  });

  [McpServerTool(Name = "get_exercise_template", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one complete exercise template by identifier.")]
  internal static Task<CallToolResult> GetExerciseTemplate(IServiceProvider services, string exercise_template_id, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidateIdentifier(exercise_template_id, nameof(exercise_template_id));
    var item = await ToolResults.Client(services).GetExerciseTemplateAsync(exercise_template_id, cancellationToken);
    return ToolResults.Success(item, $"Returned exercise template {item.Id}.");
  });

  [McpServerTool(Name = "get_exercise_history", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one local page of exercise history between optional inclusive calendar dates; units are kilograms, meters, and seconds.")]
  internal static Task<CallToolResult> GetExerciseHistory(IServiceProvider services, string exercise_template_id, [Range(1, int.MaxValue)] int page = 1, [Range(1, 10)] int page_size = 10, DateOnly? start_date = null, DateOnly? end_date = null, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidateIdentifier(exercise_template_id, nameof(exercise_template_id));
    ToolResults.ValidatePagination(page, page_size);
    ToolResults.ValidateDetail(detail);
    if (start_date > end_date) throw new ArgumentException("start_date cannot be after end_date.", nameof(start_date));
    var result = await ToolResults.Client(services).GetExerciseHistoryAsync(exercise_template_id, page, page_size, start_date, end_date, cancellationToken);
    object items = detail == "full" ? result.Items : result.Items.Select(static entry => new { entry.WorkoutId, entry.WorkoutTitle, entry.WorkoutStartTime, entry.ExerciseTemplateId, entry.SetType }).ToArray();
    return ToolResults.Success(new { items }, $"Returned {result.Items.Count} exercise history entries.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
  });
}

internal static class ExerciseWriteTools
{
  [McpServerTool(Name = "create_exercise_template", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Create a custom exercise template.")]
  internal static Task<CallToolResult> CreateExerciseTemplate(IServiceProvider services, CreateExerciseTemplateRequest request, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    ToolValidation.Exercise(request.Exercise);
    if (dry_run) return ToolResults.Success(request, "Exercise-template payload is valid; no request was sent.", new { dry_run = true });
    var result = await ToolResults.Client(services).CreateExerciseTemplateAsync(request, cancellationToken);
    return ToolResults.Success(result, $"Created exercise template {result.Id}.", new { dry_run = false });
  });
}
