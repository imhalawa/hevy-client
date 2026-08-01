namespace Hevy.Core.UseCases;

public sealed class CreateRoutineFolderUseCase(IHevyClient client)
{
  public async Task<RoutineFolder?> ExecuteAsync(CreateRoutineFolderCommand command, bool dryRun, CancellationToken cancellationToken)
  {
    command.RoutineFolder.Validate();
    return dryRun ? null : await client.CreateRoutineFolderAsync(command, cancellationToken);
  }
}
