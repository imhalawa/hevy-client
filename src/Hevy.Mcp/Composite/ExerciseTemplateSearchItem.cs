namespace Hevy.Mcp.Composite;

internal sealed record ExerciseTemplateSearchItem(
    string Id,
    string Title,
    string Type,
    string PrimaryMuscleGroup,
    ImmutableList<string> SecondaryMuscleGroups,
    EquipmentCategory EquipmentCategory,
    bool IsCustom);
