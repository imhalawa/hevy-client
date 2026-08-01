using Hevy.Core.Exceptions;

namespace Hevy.Core.UseCases;

public sealed class UpdateWorkoutUseCase(IHevyClient client)
{
  public async Task<Workout?> ExecuteAsync(
      string workoutId,
      UpdateWorkoutCommand command,
      DateTimeOffset? expectedUpdatedAt,
      bool force,
      bool dryRun,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(workoutId)) throw new ArgumentException("An identifier is required.", nameof(workoutId));
    if (!force && expectedUpdatedAt is null) throw new ArgumentException("expected_updated_at is required unless force is true.", nameof(expectedUpdatedAt));
    command.Workout.Validate();
    if (dryRun) return null;

    if (!force && (await client.GetWorkoutAsync(workoutId, cancellationToken)).UpdatedAt != expectedUpdatedAt)
    {
      throw new HevyConflictException("The workout changed since expected_updated_at; read it again before replacing it.");
    }

    return await client.UpdateWorkoutAsync(workoutId, command, cancellationToken);
  }
}
