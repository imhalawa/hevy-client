namespace Hevy.Core.UseCases;

public sealed record CreateRoutineCommand(CreateRoutineWrite Routine)
{
  public void Validate() => MutationValidation.Routine(Routine);

  public async Task<Routine?> ExecuteAsync(IHevyClient client, bool dryRun, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(client);
    Validate();
    return dryRun ? null : await client.CreateRoutineAsync(this, cancellationToken);
  }
}
