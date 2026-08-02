using Hevy.Core.Exceptions;

namespace Hevy.Core.UseCases;

public sealed class UpdateBodyMeasurementUseCase(IHevyClient client)
{
  public async Task<BodyMeasurement?> ExecuteAsync(
      DateOnly date,
      UpdateBodyMeasurementCommand command,
      DateTimeOffset? expectedUpdatedAt,
      bool force,
      bool dryRun,
      CancellationToken cancellationToken)
  {
    if (date == DateOnly.MinValue) throw new ArgumentException("A measurement date is required.", nameof(date));
    command.Measurement.Validate();
    if (!force && expectedUpdatedAt is null) throw new ArgumentException("expected_updated_at is required unless force is true.", nameof(expectedUpdatedAt));
    if (dryRun) return null;

    if (!force)
    {
      await client.GetBodyMeasurementAsync(date, cancellationToken);
      throw new HevyConflictException("Hevy body measurements do not expose updated_at, so the guard cannot be verified; retry only with force after reviewing the current measurement.");
    }

    return await client.UpdateBodyMeasurementAsync(date, command, cancellationToken);
  }
}
