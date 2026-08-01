namespace Hevy.Core.Models;

public sealed record ExerciseTemplate(string Id, string Title, string Type, string PrimaryMuscleGroup, ImmutableList<string> SecondaryMuscleGroups, EquipmentCategory EquipmentCategory, bool IsCustom);
