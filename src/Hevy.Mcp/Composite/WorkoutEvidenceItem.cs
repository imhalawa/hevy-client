namespace Hevy.Mcp.Composite;

internal sealed record WorkoutEvidenceItem(
    string WorkoutId,
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    ImmutableList<ExerciseEvidenceItem> Exercises);
