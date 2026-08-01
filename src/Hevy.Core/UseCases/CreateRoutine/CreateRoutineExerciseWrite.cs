namespace Hevy.Core.UseCases;

public sealed record CreateRoutineExerciseWrite(string ExerciseTemplateId, long? SupersetId, int? RestSeconds, string? Notes, ImmutableList<CreateRoutineSetWrite> Sets);
