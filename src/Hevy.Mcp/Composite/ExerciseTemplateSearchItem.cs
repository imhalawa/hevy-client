using System.Globalization;
using Hevy.Core.Models;
using Hevy.Mcp.Caching;

namespace Hevy.Mcp.Composite;

internal sealed record ExerciseTemplateSearchItem(
    string Id,
    string Title,
    string Type,
    string PrimaryMuscleGroup,
    ImmutableList<string> SecondaryMuscleGroups,
    EquipmentCategory EquipmentCategory,
    bool IsCustom);
