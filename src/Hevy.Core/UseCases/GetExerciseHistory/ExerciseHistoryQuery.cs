namespace Hevy.Core.UseCases;

public sealed record ExerciseHistoryQuery(int Offset, int Limit, DateOnly? StartDate = null, DateOnly? EndDate = null, DateTimeOffset? EligibleStartTime = null, DateTimeOffset? EligibleEndTime = null)
{
  public const int MaximumLimit = 1000;

  public const int MaximumScannedItems = 1000;

  public void Validate()
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(Offset, 0, "Offset");
    ArgumentOutOfRangeException.ThrowIfLessThan(Limit, 1, "Limit");
    ArgumentOutOfRangeException.ThrowIfGreaterThan(Limit, 1000, "Limit");
    if ((long)Offset + (long)Limit > 1000)
    {
      throw new ArgumentOutOfRangeException("Offset", $"The requested history window exceeds the {1000}-item scan limit.");
    }
    if (StartDate.HasValue && EndDate.HasValue && StartDate > EndDate)
    {
      throw new ArgumentException("The start date cannot be after the end date.", "StartDate");
    }
    if (EligibleStartTime.HasValue && EligibleEndTime.HasValue && EligibleStartTime >= EligibleEndTime)
    {
      throw new ArgumentException("The eligible start time must be before the eligible end time.", "EligibleStartTime");
    }
  }

  public static int PageOffset(int page, int pageSize)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(page, 1, "page");
    ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1, "pageSize");
    long num = ((long)page - 1L) * pageSize;
    if (num > int.MaxValue || num + pageSize > 1000)
    {
      throw new ArgumentOutOfRangeException("page", $"The requested history page exceeds the {1000}-item scan limit.");
    }
    return (int)num;
  }
}
