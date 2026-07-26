using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record ExerciseTemplate(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Type,
    [property: JsonRequired] string PrimaryMuscleGroup,
    [property: JsonRequired] IReadOnlyList<string> SecondaryMuscleGroups,
    [property: JsonRequired] EquipmentCategory EquipmentCategory,
    [property: JsonRequired] bool IsCustom);

public sealed record ExerciseTemplatePage(
    [property: JsonRequired] int Page,
    [property: JsonRequired] int PageCount,
    [property: JsonRequired] IReadOnlyList<ExerciseTemplate> ExerciseTemplates);

public sealed record CreateExerciseTemplateResponse([property: JsonRequired] int Id);

public sealed record ExerciseHistoryEntry(
    [property: JsonRequired] string WorkoutId,
    [property: JsonRequired] string WorkoutTitle,
    [property: JsonRequired] DateTimeOffset WorkoutStartTime,
    [property: JsonRequired] DateTimeOffset WorkoutEndTime,
    [property: JsonRequired] string ExerciseTemplateId,
    decimal? WeightKg,
    int? Reps,
    int? DistanceMeters,
    int? DurationSeconds,
    decimal? Rpe,
    decimal? CustomMetric,
    [property: JsonRequired] string SetType);

public sealed record ExerciseHistoryResponse([property: JsonRequired] IReadOnlyList<ExerciseHistoryEntry> ExerciseHistory);

public sealed record CreateExerciseTemplateRequest(CustomExerciseWrite Exercise);

public sealed record CustomExerciseWrite(
    string Title,
    CustomExerciseType ExerciseType,
    EquipmentCategory EquipmentCategory,
    MuscleGroup MuscleGroup,
    IReadOnlyList<MuscleGroup> OtherMuscles);
