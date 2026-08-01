namespace Hevy.Core.UseCases;

public sealed record CreateRoutineWrite(string Title, long? FolderId, string Notes, ImmutableList<CreateRoutineExerciseWrite> Exercises);
