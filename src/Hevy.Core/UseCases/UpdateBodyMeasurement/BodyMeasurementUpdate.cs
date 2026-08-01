namespace Hevy.Core.UseCases;

public sealed record BodyMeasurementUpdate(decimal? WeightKg, decimal? LeanMassKg, decimal? FatPercent, decimal? NeckCm, decimal? ShoulderCm, decimal? ChestCm, decimal? LeftBicepCm, decimal? RightBicepCm, decimal? LeftForearmCm, decimal? RightForearmCm, decimal? Abdomen, decimal? Waist, decimal? Hips, decimal? LeftThigh, decimal? RightThigh, decimal? LeftCalf, decimal? RightCalf)
{
  public void Validate(DateOnly date) => MutationValidation.Measurement(date, Values());

  public async Task<BodyMeasurement?> ExecuteAsync(
      IHevyClient client,
      DateOnly date,
      DateTimeOffset? expectedUpdatedAt,
      bool force,
      bool dryRun,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(client);
    Validate(date);
    MutationValidation.Guard(expectedUpdatedAt, force);
    if (dryRun) return null;

    if (!force)
    {
      await client.GetBodyMeasurementAsync(date, cancellationToken);
      throw new Hevy.Core.Exceptions.HevyConflictException("Hevy body measurements do not expose updated_at, so the guard cannot be verified; retry only with force after reviewing the current measurement.");
    }

    return await client.UpdateBodyMeasurementAsync(date, this, cancellationToken);
  }

  private decimal?[] Values() => [WeightKg, LeanMassKg, FatPercent, NeckCm, ShoulderCm, ChestCm, LeftBicepCm, RightBicepCm, LeftForearmCm, RightForearmCm, Abdomen, Waist, Hips, LeftThigh, RightThigh, LeftCalf, RightCalf];
}
