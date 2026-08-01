namespace Hevy.Core.UseCases;

public sealed record CreateWorkoutExerciseWrite(string ExerciseTemplateId, long? SupersetId, string? Notes, ImmutableList<CreateWorkoutSetWrite> Sets);
