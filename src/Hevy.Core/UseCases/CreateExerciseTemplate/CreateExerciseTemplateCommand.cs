namespace Hevy.Core.UseCases;

public sealed record CreateExerciseTemplateCommand(CustomExerciseWrite Exercise)
{
  public void Validate() => MutationValidation.Exercise(Exercise);

  public async Task<ExerciseTemplate?> ExecuteAsync(IHevyClient client, bool dryRun, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(client);
    Validate();
    return dryRun ? null : await client.CreateExerciseTemplateAsync(this, cancellationToken);
  }
}
