namespace Hevy.Core.UseCases;

public sealed record UpdateWorkoutCommand(UpdateWorkoutWrite Workout)
{
  public void Validate() => MutationValidation.Workout(Workout);

  public async Task<Workout?> ExecuteAsync(
      IHevyClient client,
      string workoutId,
      DateTimeOffset? expectedUpdatedAt,
      bool force,
      bool dryRun,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(client);
    MutationValidation.Identifier(workoutId, nameof(workoutId));
    MutationValidation.Guard(expectedUpdatedAt, force);
    Validate();
    if (dryRun) return null;

    if (!force && (await client.GetWorkoutAsync(workoutId, cancellationToken)).UpdatedAt != expectedUpdatedAt)
    {
      throw new Hevy.Core.Exceptions.HevyConflictException("The workout changed since expected_updated_at; read it again before replacing it.");
    }

    return await client.UpdateWorkoutAsync(workoutId, this, cancellationToken);
  }
}
