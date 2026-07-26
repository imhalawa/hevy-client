namespace Hevy.Client.Models;

public sealed record ExerciseHistoryWindowRequest(
    int Offset,
    int Limit,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    DateTimeOffset? EligibleStartTime = null,
    DateTimeOffset? EligibleEndTime = null)
{
  public const int MaximumLimit = 1_000;
  public const int MaximumScannedItems = 1_000;

  public void Validate()
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(Offset, 0);
    ArgumentOutOfRangeException.ThrowIfLessThan(Limit, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(Limit, MaximumLimit);
    if ((long)Offset + Limit > MaximumScannedItems)
    {
      throw new ArgumentOutOfRangeException(nameof(Offset), $"The requested history window exceeds the {MaximumScannedItems}-item scan limit.");
    }
    if (StartDate is not null && EndDate is not null && StartDate > EndDate)
    {
      throw new ArgumentException("The start date cannot be after the end date.", nameof(StartDate));
    }
    if (EligibleStartTime is not null && EligibleEndTime is not null && EligibleStartTime >= EligibleEndTime)
    {
      throw new ArgumentException("The eligible start time must be before the eligible end time.", nameof(EligibleStartTime));
    }
  }

  public static int PageOffset(int page, int pageSize)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
    ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
    var offset = ((long)page - 1) * pageSize;
    if (offset > int.MaxValue || offset + pageSize > MaximumScannedItems)
    {
      throw new ArgumentOutOfRangeException(nameof(page), $"The requested history page exceeds the {MaximumScannedItems}-item scan limit.");
    }
    return (int)offset;
  }
}

public sealed record ExerciseHistoryWindow(
    IReadOnlyList<ExerciseHistoryEntry> Items,
    bool Truncated,
    int ScannedItemCount,
    string? TruncationReason = null)
{
  public const string ItemSafetyCap = "item_safety_cap";
  public const string ByteSafetyCap = "byte_safety_cap";
}
