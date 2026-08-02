using System.Text.Json.Serialization.Metadata;
using Hevy.Client.Models;

namespace Hevy.Client.Http;

internal sealed class HistoryReadState(
    ExerciseHistoryQuery request,
    JsonTypeInfo<ExerciseHistoryEntryResponse> entryTypeInfo)
{
  private readonly List<ExerciseHistoryEntry> items = new(request.Limit);
  private int eligibleItemCount;

  internal int ScannedItemCount { get; private set; }
  internal JsonTypeInfo<ExerciseHistoryEntryResponse> EntryTypeInfo { get; } = entryTypeInfo;

  internal ExerciseHistoryWindow? Add(ExerciseHistoryEntry entry)
  {
    ScannedItemCount++;
    if (!IsEligible(entry)) return null;
    if (eligibleItemCount++ < request.Offset) return null;
    if (items.Count < request.Limit)
    {
      items.Add(entry);
      return null;
    }

    return Truncated(null);
  }

  internal ExerciseHistoryWindow Complete() => new(items.ToImmutableList(), false, ScannedItemCount);

  internal ExerciseHistoryWindow Truncated(string? reason) => new(items.ToImmutableList(), true, ScannedItemCount, reason);

  private bool IsEligible(ExerciseHistoryEntry entry) =>
      (request.EligibleStartTime is null || entry.WorkoutStartTime >= request.EligibleStartTime) &&
      (request.EligibleEndTime is null || entry.WorkoutStartTime < request.EligibleEndTime);
}
