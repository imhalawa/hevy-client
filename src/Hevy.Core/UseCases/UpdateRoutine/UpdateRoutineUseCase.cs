using Hevy.Core.Exceptions;

namespace Hevy.Core.UseCases;

public sealed class UpdateRoutineUseCase(IHevyClient client)
{
  public async Task<Routine?> ExecuteAsync(
      string routineId,
      UpdateRoutineCommand command,
      DateTimeOffset? expectedUpdatedAt,
      bool force,
      bool dryRun,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(routineId)) throw new ArgumentException("An identifier is required.", nameof(routineId));
    if (!force && expectedUpdatedAt is null) throw new ArgumentException("expected_updated_at is required unless force is true.", nameof(expectedUpdatedAt));
    command.Routine.Validate();
    if (dryRun) return null;

    if (!force && (await client.GetRoutineAsync(routineId, cancellationToken)).UpdatedAt != expectedUpdatedAt)
    {
      throw new HevyConflictException("The routine changed since expected_updated_at; read it again before replacing it.");
    }

    return await client.UpdateRoutineAsync(routineId, command, cancellationToken);
  }
}
