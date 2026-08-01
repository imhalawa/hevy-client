namespace Hevy.Core.UseCases;

public sealed record UpdateWorkoutExerciseWrite(
    string ExerciseTemplateId,
    long? SupersetId,
    string? Notes,
    ImmutableList<UpdateWorkoutSetWrite> Sets);
