namespace Hevy.Client.Models;

public sealed record UpdateBodyMeasurementRequest(decimal? WeightKg, decimal? LeanMassKg, decimal? FatPercent, decimal? NeckCm, decimal? ShoulderCm, decimal? ChestCm, decimal? LeftBicepCm, decimal? RightBicepCm, decimal? LeftForearmCm, decimal? RightForearmCm, decimal? Abdomen, decimal? Waist, decimal? Hips, decimal? LeftThigh, decimal? RightThigh, decimal? LeftCalf, decimal? RightCalf)
{
  public static implicit operator UpdateBodyMeasurementRequest(UpdateBodyMeasurementCommand value) =>
      new(value.Measurement.WeightKg, value.Measurement.LeanMassKg, value.Measurement.FatPercent, value.Measurement.NeckCm, value.Measurement.ShoulderCm, value.Measurement.ChestCm, value.Measurement.LeftBicepCm, value.Measurement.RightBicepCm, value.Measurement.LeftForearmCm, value.Measurement.RightForearmCm, value.Measurement.Abdomen, value.Measurement.Waist, value.Measurement.Hips, value.Measurement.LeftThigh, value.Measurement.RightThigh, value.Measurement.LeftCalf, value.Measurement.RightCalf);

  public static implicit operator UpdateBodyMeasurementCommand(UpdateBodyMeasurementRequest value) =>
      new(new UpdateBodyMeasurementWrite(value.WeightKg, value.LeanMassKg, value.FatPercent, value.NeckCm, value.ShoulderCm, value.ChestCm, value.LeftBicepCm, value.RightBicepCm, value.LeftForearmCm, value.RightForearmCm, value.Abdomen, value.Waist, value.Hips, value.LeftThigh, value.RightThigh, value.LeftCalf, value.RightCalf));
}
