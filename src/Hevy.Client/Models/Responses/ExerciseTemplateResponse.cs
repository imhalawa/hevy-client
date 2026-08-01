using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record ExerciseTemplateResponse([property: JsonRequired] string Id, [property: JsonRequired] string Title, [property: JsonRequired] string Type, [property: JsonRequired] string PrimaryMuscleGroup, [property: JsonRequired] ImmutableList<string> SecondaryMuscleGroups, [property: JsonRequired] EquipmentCategoryApi EquipmentCategory, [property: JsonRequired] bool IsCustom)
{
  internal ExerciseTemplate ToDomain() => new(Id, Title, Type, PrimaryMuscleGroup, SecondaryMuscleGroups, (EquipmentCategory)EquipmentCategory, IsCustom);
}
