namespace Hevy.Core.UseCases;

public sealed record UpdateRoutineCommand(UpdateRoutineWrite Routine)
{
  public void Validate() => MutationValidation.Routine(Routine);

  public async Task<Routine?> ExecuteAsync(
      IHevyClient client,
      string routineId,
      DateTimeOffset? expectedUpdatedAt,
      bool force,
      bool dryRun,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(client);
    MutationValidation.Identifier(routineId, nameof(routineId));
    MutationValidation.Guard(expectedUpdatedAt, force);
    Validate();
    if (dryRun) return null;

    if (!force && (await client.GetRoutineAsync(routineId, cancellationToken)).UpdatedAt != expectedUpdatedAt)
    {
      throw new Hevy.Core.Exceptions.HevyConflictException("The routine changed since expected_updated_at; read it again before replacing it.");
    }

    return await client.UpdateRoutineAsync(routineId, this, cancellationToken);
  }
}
