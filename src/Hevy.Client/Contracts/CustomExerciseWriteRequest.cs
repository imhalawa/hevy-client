using System.Collections.Immutable;

namespace Hevy.Client.Contracts;

public sealed record CustomExerciseWriteRequest(string Title, CustomExerciseTypeApi ExerciseType, EquipmentCategoryApi EquipmentCategory, MuscleGroupApi MuscleGroup, ImmutableList<MuscleGroupApi> OtherMuscles);
