using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record ExerciseTemplateResponse([property: JsonRequired] string Id, [property: JsonRequired] string Title, [property: JsonRequired] string Type, [property: JsonRequired] string PrimaryMuscleGroup, [property: JsonRequired] ImmutableList<string> SecondaryMuscleGroups, [property: JsonRequired] EquipmentCategoryApi EquipmentCategory, [property: JsonRequired] bool IsCustom) : IHevyResponse
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Id)) throw new JsonException();
    if (SecondaryMuscleGroups.Any(static muscle => muscle is null)) throw new JsonException();
  }

  internal ExerciseTemplate ToDomain() => new(Id, Title, Type, PrimaryMuscleGroup, SecondaryMuscleGroups, (EquipmentCategory)EquipmentCategory, IsCustom);
}
