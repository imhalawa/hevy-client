namespace Hevy.Core.UseCases;

public sealed record UpdateWorkoutWrite(
    string Title,
    string? Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool IsPrivate,
    ImmutableList<UpdateWorkoutExerciseWrite> Exercises);
