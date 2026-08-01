namespace Hevy.Core.UseCases;

public sealed class CreateWorkoutUseCase(IHevyClient client)
{
  public async Task<Workout?> ExecuteAsync(CreateWorkoutCommand command, bool dryRun, CancellationToken cancellationToken)
  {
    command.Workout.Validate();
    return dryRun ? null : await client.CreateWorkoutAsync(command, cancellationToken);
  }
}
