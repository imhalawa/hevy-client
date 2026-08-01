namespace Hevy.Core.UseCases;

public sealed class CreateExerciseTemplateUseCase(IHevyClient client)
{
  public async Task<ExerciseTemplate?> ExecuteAsync(CreateExerciseTemplateCommand command, bool dryRun, CancellationToken cancellationToken)
  {
    command.Exercise.Validate();
    return dryRun ? null : await client.CreateExerciseTemplateAsync(command, cancellationToken);
  }
}
