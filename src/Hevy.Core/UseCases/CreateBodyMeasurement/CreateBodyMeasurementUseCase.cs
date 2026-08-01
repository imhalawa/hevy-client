namespace Hevy.Core.UseCases;

public sealed class CreateBodyMeasurementUseCase(IHevyClient client)
{
  public async Task<BodyMeasurement?> ExecuteAsync(CreateBodyMeasurementCommand command, bool dryRun, CancellationToken cancellationToken)
  {
    command.Measurement.Validate();
    return dryRun ? null : await client.CreateBodyMeasurementAsync(command, cancellationToken);
  }
}
