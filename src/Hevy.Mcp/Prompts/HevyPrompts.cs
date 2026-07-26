using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Prompts;

internal sealed class HevyPrompts
{
  [McpServerPrompt(Name = "analyze_recent_training")]
  [Description("Guide an evidence-cited analysis of recent training from deterministic Hevy calculations.")]
  internal static ChatMessage AnalyzeRecentTraining(
      [Description("Number of UTC weeks to analyze, from 1 through 52.")] int weeks = 4) =>
      new(ChatRole.User,
          $"Call summarize_training with weeks={weeks}. If its result is truncated, follow its continuation before drawing conclusions. " +
          "Use get_workout_evidence when more detail is needed. Clearly separate deterministic returned facts from interpretation. " +
          "Cite the returned evidence workout or exercise identifiers and UTC timestamps for every factual claim; do not invent evidence.");

  [McpServerPrompt(Name = "create_completed_workout_from_routine")]
  [Description("Guide creation of a completed workout from a routine without inventing performed results.")]
  internal static ChatMessage CreateCompletedWorkoutFromRoutine(
      [Description("Hevy routine identifier to use as the planned structure.")] string routine_id) =>
      new(ChatRole.User,
          $"Call get_routine for routine_id={routine_id} and use it only as the planned structure. " +
          "Collect the user's actual completed-set results for every performed set and the actual end time before any mutation. " +
          "Do not invent weights, repetitions, set completion, RPE, start time, or end time. Confirm omissions explicitly, then call create_workout with only the reported completed workout data.");
}
