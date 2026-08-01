namespace Hevy.Client.Models;

public sealed record UpdateBodyMeasurementRequest(decimal? WeightKg, decimal? LeanMassKg, decimal? FatPercent, decimal? NeckCm, decimal? ShoulderCm, decimal? ChestCm, decimal? LeftBicepCm, decimal? RightBicepCm, decimal? LeftForearmCm, decimal? RightForearmCm, decimal? Abdomen, decimal? Waist, decimal? Hips, decimal? LeftThigh, decimal? RightThigh, decimal? LeftCalf, decimal? RightCalf)
{
  public static implicit operator UpdateBodyMeasurementRequest(BodyMeasurementUpdate value) =>
      new(value.WeightKg, value.LeanMassKg, value.FatPercent, value.NeckCm, value.ShoulderCm, value.ChestCm, value.LeftBicepCm, value.RightBicepCm, value.LeftForearmCm, value.RightForearmCm, value.Abdomen, value.Waist, value.Hips, value.LeftThigh, value.RightThigh, value.LeftCalf, value.RightCalf);

  public static implicit operator BodyMeasurementUpdate(UpdateBodyMeasurementRequest value) =>
      new(value.WeightKg, value.LeanMassKg, value.FatPercent, value.NeckCm, value.ShoulderCm, value.ChestCm, value.LeftBicepCm, value.RightBicepCm, value.LeftForearmCm, value.RightForearmCm, value.Abdomen, value.Waist, value.Hips, value.LeftThigh, value.RightThigh, value.LeftCalf, value.RightCalf);
}
