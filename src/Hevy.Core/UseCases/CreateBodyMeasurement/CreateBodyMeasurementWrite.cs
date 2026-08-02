namespace Hevy.Core.UseCases;

public sealed record CreateBodyMeasurementWrite(DateOnly Date, decimal? WeightKg, decimal? LeanMassKg, decimal? FatPercent, decimal? NeckCm, decimal? ShoulderCm, decimal? ChestCm, decimal? LeftBicepCm, decimal? RightBicepCm, decimal? LeftForearmCm, decimal? RightForearmCm, decimal? Abdomen, decimal? Waist, decimal? Hips, decimal? LeftThigh, decimal? RightThigh, decimal? LeftCalf, decimal? RightCalf)
{
  public void Validate()
  {
    if (Date == DateOnly.MinValue) throw new ArgumentException("A measurement date is required.", nameof(Date));
    if (Values().Any(static value => value is < 0)) throw new ArgumentOutOfRangeException(nameof(WeightKg), "Measurement values cannot be negative.");
  }

  private decimal?[] Values() => [WeightKg, LeanMassKg, FatPercent, NeckCm, ShoulderCm, ChestCm, LeftBicepCm, RightBicepCm, LeftForearmCm, RightForearmCm, Abdomen, Waist, Hips, LeftThigh, RightThigh, LeftCalf, RightCalf];
}
