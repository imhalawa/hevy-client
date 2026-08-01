using Hevy.Core.Models;

namespace Hevy.Mcp.Tools;

internal sealed record WorkoutListItem(
    string Id,
    string Title,
    string RoutineId,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset UpdatedAt,
    DateTimeOffset CreatedAt,
    ImmutableList<WorkoutExercise>? Exercises = null);
