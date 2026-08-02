using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class CompositeTools
{
  [McpServerTool(Name = "search_routines", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<CompositeResult<RoutineSearchItem>, NoMeta>))]
  [Description("Search the complete bounded routine catalog by invariant case-folded, whitespace-normalized title. Returns compact identifiers and titles.")]
  internal static async Task<CallToolResult> SearchRoutines(
      IServiceProvider services,
      string query,
      [Range(1, 1_000)] int limit = 100,
      string? continuation = null,
      CancellationToken cancellationToken = default)
  {
    var result = await Search(services).SearchRoutinesAsync(query, limit, continuation, cancellationToken);
    return ToolResults.Success(result, $"Returned {result.Items.Count} matching routines.");
  }

  [McpServerTool(Name = "search_exercise_templates", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<CompositeResult<ExerciseTemplateSearchItem>, NoMeta>))]
  [Description("Search the complete bounded exercise-template catalog by normalized title and optional exact equipment or primary/secondary muscle filters.")]
  internal static async Task<CallToolResult> SearchExerciseTemplates(
      IServiceProvider services,
      string query,
      string? equipment = null,
      string? muscle = null,
      [Range(1, 1_000)] int limit = 100,
      string? continuation = null,
      CancellationToken cancellationToken = default)
  {
    var result = await Search(services).SearchExerciseTemplatesAsync(query, equipment, muscle, limit, continuation, cancellationToken);
    return ToolResults.Success(result, $"Returned {result.Items.Count} matching exercise templates.");
  }

  [McpServerTool(Name = "get_workout_evidence", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<WorkoutEvidenceResult, NoMeta>))]
  [Description("Get bounded workout identifiers, UTC timestamps, exercise identifiers, and counted weight-times-repetition evidence. Defaults to four UTC weeks.")]
  internal static async Task<CallToolResult> GetWorkoutEvidence(
      IServiceProvider services,
      [Range(1, 52)] int weeks = 4,
      DateTimeOffset? range_end_utc = null,
      [Range(1, 1_000)] int limit = 100,
      string? continuation = null,
      CancellationToken cancellationToken = default)
  {
    var result = await ToolResults.Service<TrainingAnalysisUseCase>(services).GetWorkoutEvidenceAsync(weeks, range_end_utc, limit, continuation, cancellationToken);
    return ToolResults.Success(result, $"Returned {result.Items.Count} workout evidence records.");
  }

  [McpServerTool(Name = "summarize_training", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<TrainingSummary, NoMeta>))]
  [Description("Calculate deterministic UTC-period frequency, weight-times-repetition volume, progression observations, missing-period gaps, and body-measurement deltas with evidence identifiers. Partial chunks are explicitly scoped and composable; no coaching is generated.")]
  internal static async Task<CallToolResult> SummarizeTraining(
      IServiceProvider services,
      [Range(1, 52)] int weeks = 4,
      DateTimeOffset? range_end_utc = null,
      [Range(1, 1_000)] int limit = 100,
      string? continuation = null,
      CancellationToken cancellationToken = default)
  {
    var result = await ToolResults.Service<TrainingAnalysisUseCase>(services).SummarizeTrainingAsync(weeks, range_end_utc, limit, continuation, cancellationToken);
    return ToolResults.Success(result, $"Calculated a deterministic {result.Weeks}-week {result.MetricScope} training summary chunk from {result.ChunkWorkoutFrequency} workouts.");
  }

  [McpServerTool(Name = "summarize_exercise_history", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<ExerciseHistorySummary, NoMeta>))]
  [Description("Calculate deterministic bounded exercise-history volume and progression with supporting workout identifiers and UTC timestamps. One official response is streamed with 1,000-item and 16 MiB safety limits; no coaching is generated.")]
  internal static async Task<CallToolResult> SummarizeExerciseHistory(
      IServiceProvider services,
      string exercise_template_id,
      [Range(1, 52)] int weeks = 4,
      DateTimeOffset? range_end_utc = null,
      [Range(1, 1_000)] int limit = 100,
      string? continuation = null,
      CancellationToken cancellationToken = default)
  {
    var result = await ToolResults.Service<TrainingAnalysisUseCase>(services).SummarizeExerciseHistoryAsync(exercise_template_id, weeks, range_end_utc, limit, continuation, cancellationToken);
    return ToolResults.Success(result, $"Calculated {result.MetricScope} exercise history from {result.ChunkEntryCount} entries.");
  }

  private static SearchUseCase Search(IServiceProvider services)
  {
    var client = ToolResults.Client(services);
    return new SearchUseCase(
        (page, cancellationToken) => client.GetRoutinesAsync(page, 10, cancellationToken),
        (page, cancellationToken) => client.GetExerciseTemplatesAsync(page, 10, cancellationToken));
  }
}
