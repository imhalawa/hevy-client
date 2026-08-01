namespace Hevy.Core.UseCases;

public sealed record CreateWorkoutCommand(CreateWorkoutWrite Workout)
{
  public void Validate() => MutationValidation.Workout(Workout);

  public async Task<Workout?> ExecuteAsync(IHevyClient client, bool dryRun, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(client);
    Validate();
    return dryRun ? null : await client.CreateWorkoutAsync(this, cancellationToken);
  }
}
