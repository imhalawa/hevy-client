namespace Hevy.Mcp.Tools;

internal sealed record RoutineListItem(
    string Id,
    string Title,
    long? FolderId,
    DateTimeOffset UpdatedAt,
    DateTimeOffset CreatedAt,
    ImmutableList<RoutineExercise>? Exercises = null);
