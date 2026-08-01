using System;

namespace Hevy.Core.Models;

public readonly record struct WorkoutRpe
{
  public decimal Value { get; }

  public WorkoutRpe(decimal value)
  {
    if (!IsValid(value))
    {
      throw new ArgumentOutOfRangeException("value", value, "RPE must be one of Hevy's documented values.");
    }
    Value = value;
  }

  public static bool IsValid(decimal value)
  {
    if (value <= 8m)
    {
      if (value <= 7m)
      {
        if (value == 6m || value == 7m)
        {
          goto IL_00b5;
        }
      }
      else if (value == 7.5m || value == 8m)
      {
        goto IL_00b5;
      }
    }
    else if (value <= 9m)
    {
      if (value == 8.5m || value == 9m)
      {
        goto IL_00b5;
      }
    }
    else if (value == 9.5m || value == 10m)
    {
      goto IL_00b5;
    }
    return false;
  IL_00b5:
    return true;
  }
}
