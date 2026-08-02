namespace Hevy.Mcp.Tools;

internal sealed record ExerciseHistoryContinuation(
    string ExerciseTemplateId,
    int Page,
    int PageSize,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Detail);
