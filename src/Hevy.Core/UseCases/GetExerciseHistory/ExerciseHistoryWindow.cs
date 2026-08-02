namespace Hevy.Core.UseCases;

public sealed record ExerciseHistoryWindow(ImmutableList<ExerciseHistoryEntry> Items, bool Truncated, int ScannedItemCount, string? TruncationReason = null)
{
  public const string ItemSafetyCap = "item_safety_cap";

  public const string ByteSafetyCap = "byte_safety_cap";
}
