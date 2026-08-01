namespace Hevy.Core.UseCases;

public sealed record ExerciseHistoryQuery(int Offset, int Limit, DateOnly? StartDate = null, DateOnly? EndDate = null, DateTimeOffset? EligibleStartTime = null, DateTimeOffset? EligibleEndTime = null)
{
  public const int MaximumLimit = 1000;

  public const int MaximumScannedItems = 1000;

  public void Validate()
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(Offset, 0, nameof(Offset));
    ArgumentOutOfRangeException.ThrowIfLessThan(Limit, 1, nameof(Limit));
    ArgumentOutOfRangeException.ThrowIfGreaterThan(Limit, MaximumLimit, nameof(Limit));
    if ((long)Offset + Limit > MaximumScannedItems)
    {
      throw new ArgumentOutOfRangeException(nameof(Offset), $"The requested history window exceeds the {MaximumScannedItems}-item scan limit.");
    }
    if (StartDate.HasValue && EndDate.HasValue && StartDate > EndDate)
    {
      throw new ArgumentException("The start date cannot be after the end date.", nameof(StartDate));
    }
    if (EligibleStartTime.HasValue && EligibleEndTime.HasValue && EligibleStartTime >= EligibleEndTime)
    {
      throw new ArgumentException("The eligible start time must be before the eligible end time.", nameof(EligibleStartTime));
    }
  }

  public static int PageOffset(int page, int pageSize)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(page, 1, nameof(page));
    ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1, nameof(pageSize));
    var offset = ((long)page - 1) * pageSize;
    if (offset > int.MaxValue || offset + pageSize > MaximumScannedItems)
    {
      throw new ArgumentOutOfRangeException(nameof(page), $"The requested history page exceeds the {MaximumScannedItems}-item scan limit.");
    }
    return (int)offset;
  }
}
