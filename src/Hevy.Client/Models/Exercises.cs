namespace Hevy.Client.Models;

public sealed record ExerciseTemplate(
    string Id,
    string Title,
    string Type,
    string PrimaryMuscleGroup,
    IReadOnlyList<string> SecondaryMuscleGroups,
    EquipmentCategory EquipmentCategory,
    bool IsCustom);

public sealed record ExerciseTemplatePage(int Page, int PageCount, IReadOnlyList<ExerciseTemplate> ExerciseTemplates);

public sealed record CreateExerciseTemplateResponse(int Id);

public sealed record ExerciseHistoryEntry(
    string WorkoutId,
    string WorkoutTitle,
    DateTimeOffset WorkoutStartTime,
    DateTimeOffset WorkoutEndTime,
    string ExerciseTemplateId,
    decimal? WeightKg,
    decimal? Reps,
    decimal? DistanceMeters,
    decimal? DurationSeconds,
    decimal? Rpe,
    decimal? CustomMetric,
    string SetType)
    : SetMetrics(WeightKg, Reps, DistanceMeters, DurationSeconds, Rpe, CustomMetric);

public sealed record ExerciseHistoryResponse(IReadOnlyList<ExerciseHistoryEntry> ExerciseHistory);

public sealed record CreateExerciseTemplateRequest(CustomExerciseWrite Exercise);

public sealed record CustomExerciseWrite(
    string Title,
    CustomExerciseType ExerciseType,
    EquipmentCategory EquipmentCategory,
    MuscleGroup MuscleGroup,
    IReadOnlyList<MuscleGroup> OtherMuscles);
