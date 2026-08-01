using System.ComponentModel;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class ExerciseWriteTools
{
  [McpServerTool(Name = "create_exercise_template", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<CreateExerciseTemplateRequest, ExerciseTemplate>, MutationMeta>))]
  [Description("Create a custom exercise template.")]
  internal static Task<CallToolResult> CreateExerciseTemplate(IServiceProvider services, CreateExerciseTemplateRequest request, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    CreateExerciseTemplateCommand command = request;
    if (!dry_run) ToolResults.Cache(services)?.InvalidateExerciseTemplates();
    var result = await command.ExecuteAsync(ToolResults.Client(services), dry_run, cancellationToken);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateExerciseTemplateRequest, ExerciseTemplate>(request), "Exercise-template payload is valid; no request was sent.", ToolResults.DryRunMeta());
    ArgumentNullException.ThrowIfNull(result);
    return ToolResults.Success(ToolResults.MutationResult<CreateExerciseTemplateRequest, ExerciseTemplate>(result), $"Created exercise template {result.Id}.", new MutationMeta(false));
  });
}
