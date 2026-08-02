namespace Hevy.Client.Models;

public sealed record CustomExerciseWriteRequest(string Title, CustomExerciseTypeApi ExerciseType, EquipmentCategoryApi EquipmentCategory, MuscleGroupApi MuscleGroup, ImmutableList<MuscleGroupApi> OtherMuscles)
{
  internal static CustomExerciseWriteRequest From(CustomExerciseWrite value) =>
      new(value.Title, (CustomExerciseTypeApi)value.ExerciseType, (EquipmentCategoryApi)value.EquipmentCategory, (MuscleGroupApi)value.MuscleGroup, value.OtherMuscles.Select(static muscle => (MuscleGroupApi)muscle).ToImmutableList());
}
