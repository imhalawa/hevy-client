namespace Hevy.Core.UseCases;

public sealed record UpdateRoutineExerciseWrite(string ExerciseTemplateId, long? SupersetId, int? RestSeconds, string? Notes, ImmutableList<UpdateRoutineSetWrite> Sets);
