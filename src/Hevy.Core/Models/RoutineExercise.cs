namespace Hevy.Core.Models;

public sealed record RoutineExercise(int Index, string Title, string RestSeconds, string Notes, string ExerciseTemplateId, long? SupersetId, ImmutableList<RoutineSet> Sets);
