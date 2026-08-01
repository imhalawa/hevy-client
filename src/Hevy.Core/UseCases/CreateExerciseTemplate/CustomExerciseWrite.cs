using System.Collections.Immutable;

namespace Hevy.Core.UseCases;

public sealed record CustomExerciseWrite(string Title, CustomExerciseType ExerciseType, EquipmentCategory EquipmentCategory, MuscleGroup MuscleGroup, ImmutableList<MuscleGroup> OtherMuscles);
