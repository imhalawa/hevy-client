using System.ComponentModel;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class ExerciseWriteTools
{
  [McpServerTool(Name = "create_exercise_template", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<CreateExerciseTemplateRequest, ExerciseTemplate>, MutationMeta>))]
  [Description("Create a custom exercise template.")]
  internal static async Task<CallToolResult> CreateExerciseTemplate(IServiceProvider services, CreateExerciseTemplateCommand request, bool dry_run = false, CancellationToken cancellationToken = default)
  {
    var result = await new CreateExerciseTemplateUseCase(ToolResults.Client(services)).ExecuteAsync(request, dry_run, cancellationToken);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateExerciseTemplateRequest, ExerciseTemplate>((CreateExerciseTemplateRequest)request), "Exercise-template payload is valid; no request was sent.", ToolResults.DryRunMeta());
    var exercise = result ?? throw new InvalidOperationException("The create-exercise-template use case returned no result.");
    return ToolResults.Success(ToolResults.MutationResult<CreateExerciseTemplateRequest, ExerciseTemplate>(exercise), $"Created exercise template {exercise.Id}.", new MutationMeta(false));
  }
}
