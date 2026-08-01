namespace Hevy.Core.UseCases;

public sealed record NewBodyMeasurement(DateOnly Date, decimal? WeightKg, decimal? LeanMassKg, decimal? FatPercent, decimal? NeckCm, decimal? ShoulderCm, decimal? ChestCm, decimal? LeftBicepCm, decimal? RightBicepCm, decimal? LeftForearmCm, decimal? RightForearmCm, decimal? Abdomen, decimal? Waist, decimal? Hips, decimal? LeftThigh, decimal? RightThigh, decimal? LeftCalf, decimal? RightCalf)
{
  public void Validate() => MutationValidation.Measurement(Date, Values());

  public async Task<BodyMeasurement?> ExecuteAsync(IHevyClient client, bool dryRun, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(client);
    Validate();
    return dryRun ? null : await client.CreateBodyMeasurementAsync(this, cancellationToken);
  }

  private decimal?[] Values() => [WeightKg, LeanMassKg, FatPercent, NeckCm, ShoulderCm, ChestCm, LeftBicepCm, RightBicepCm, LeftForearmCm, RightForearmCm, Abdomen, Waist, Hips, LeftThigh, RightThigh, LeftCalf, RightCalf];
}
