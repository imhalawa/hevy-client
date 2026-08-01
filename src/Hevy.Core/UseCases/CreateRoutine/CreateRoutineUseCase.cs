namespace Hevy.Core.UseCases;

public sealed class CreateRoutineUseCase(IHevyClient client)
{
  public async Task<Routine?> ExecuteAsync(CreateRoutineCommand command, bool dryRun, CancellationToken cancellationToken)
  {
    command.Routine.Validate();
    return dryRun ? null : await client.CreateRoutineAsync(command, cancellationToken);
  }
}
