namespace Hevy.Core.UseCases;

public sealed record WorkoutEvidenceItem(
    string WorkoutId,
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    ImmutableList<ExerciseEvidenceItem> Exercises);
