namespace Hevy.Core.UseCases;

public sealed record CustomExerciseWrite(string Title, CustomExerciseType ExerciseType, EquipmentCategory EquipmentCategory, MuscleGroup MuscleGroup, ImmutableList<MuscleGroup> OtherMuscles)
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Title)) throw new ArgumentException("An exercise title is required.", nameof(Title));
    var hasInvalidEnumValue = !Enum.IsDefined(ExerciseType) ||
        !Enum.IsDefined(EquipmentCategory) ||
        !Enum.IsDefined(MuscleGroup) ||
        OtherMuscles.Any(static muscle => !Enum.IsDefined(muscle));
    if (hasInvalidEnumValue)
    {
      throw new ArgumentOutOfRangeException(nameof(ExerciseType), "Exercise fields must use documented enum values.");
    }
  }
}
