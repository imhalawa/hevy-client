namespace Hevy.Core.UseCases;

public sealed class GetExerciseHistoryUseCase(IHevyClient client)
{
  public async Task<ExerciseHistoryWindow> ExecuteAsync(
      string exerciseTemplateId,
      int page,
      int pageSize,
      DateOnly? startDate,
      DateOnly? endDate,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(exerciseTemplateId)) throw new ArgumentException("An exercise template identifier is required.", nameof(exerciseTemplateId));
    var query = new ExerciseHistoryQuery(ExerciseHistoryQuery.PageOffset(page, pageSize), pageSize, startDate, endDate);
    query.Validate();
    return await client.GetExerciseHistoryWindowAsync(exerciseTemplateId, query, cancellationToken);
  }
}
