namespace Hevy.Core.Models;

public readonly record struct WorkoutRpe
{
  public decimal Value { get; }

  public WorkoutRpe(decimal value)
  {
    if (!IsValid(value))
    {
      throw new ArgumentOutOfRangeException(nameof(value), value, "RPE must be one of Hevy's documented values.");
    }
    Value = value;
  }

  public static bool IsValid(decimal value) =>
    value is 6m or 7m or 7.5m or 8m or 8.5m or 9m or 9.5m or 10m;
}
