namespace Hevy.Core.UseCases;

public sealed record CreateRoutineFolderCommand(RoutineFolderWrite RoutineFolder)
{
  public void Validate()
  {
    ArgumentNullException.ThrowIfNull(RoutineFolder);
    MutationValidation.Required(RoutineFolder.Title, "routine folder title");
  }

  public async Task<RoutineFolder?> ExecuteAsync(IHevyClient client, bool dryRun, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(client);
    Validate();
    return dryRun ? null : await client.CreateRoutineFolderAsync(this, cancellationToken);
  }
}
