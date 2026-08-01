using System.Collections.Immutable;

namespace Hevy.Core.Models;

public sealed record WorkoutExercise(int Index, string Title, string Notes, string ExerciseTemplateId, long? SupersetId, ImmutableList<WorkoutSet> Sets);
