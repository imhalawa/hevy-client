using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record Routine(
    [property: JsonRequired] string Id,
    string Title,
    long? FolderId,
    [property: JsonRequired] DateTimeOffset UpdatedAt,
    [property: JsonRequired] DateTimeOffset CreatedAt,
    [property: JsonRequired] IReadOnlyList<RoutineExercise> Exercises);

public sealed record RoutineExercise(
    int Index,
    string Title,
    string RestSeconds,
    string Notes,
    [property: JsonRequired] string ExerciseTemplateId,
    [property: JsonPropertyName("supersets_id")] long? SupersetId,
    [property: JsonRequired] IReadOnlyList<RoutineSet> Sets);

public sealed record RoutineSet(
    int Index,
    string Type,
    decimal? WeightKg,
    decimal? Reps,
    decimal? DistanceMeters,
    decimal? DurationSeconds,
    decimal? Rpe,
    decimal? CustomMetric,
    RepRange? RepRange)
    : SetMetrics(WeightKg, Reps, DistanceMeters, DurationSeconds, Rpe, CustomMetric);

public sealed record RoutinePage(
    [property: JsonRequired] int Page,
    [property: JsonRequired] int PageCount,
    [property: JsonRequired] IReadOnlyList<Routine> Routines);

public sealed record RoutineResponse([property: JsonRequired] Routine Routine);

public sealed record RoutineFolder(
    [property: JsonRequired] long Id,
    int Index,
    string Title,
    [property: JsonRequired] DateTimeOffset UpdatedAt,
    [property: JsonRequired] DateTimeOffset CreatedAt);

public sealed record RoutineFolderPage(
    [property: JsonRequired] int Page,
    [property: JsonRequired] int PageCount,
    [property: JsonRequired] IReadOnlyList<RoutineFolder> RoutineFolders);

public sealed record CreateRoutineRequest(CreateRoutineWrite Routine);

public sealed record UpdateRoutineRequest(UpdateRoutineWrite Routine);

public sealed record CreateRoutineWrite(
    string Title,
    long? FolderId,
    string Notes,
    IReadOnlyList<CreateRoutineExerciseWrite> Exercises);

public sealed record UpdateRoutineWrite(
    string Title,
    string? Notes,
    IReadOnlyList<UpdateRoutineExerciseWrite> Exercises);

public sealed record CreateRoutineExerciseWrite(
    string ExerciseTemplateId,
    long? SupersetId,
    int? RestSeconds,
    string? Notes,
    IReadOnlyList<CreateRoutineSetWrite> Sets);

public sealed record UpdateRoutineExerciseWrite(
    string ExerciseTemplateId,
    long? SupersetId,
    int? RestSeconds,
    string? Notes,
    IReadOnlyList<UpdateRoutineSetWrite> Sets);

public sealed record CreateRoutineSetWrite(
    [property: JsonConverter(typeof(SetTypeJsonConverter))] SetType Type,
    decimal? WeightKg,
    int? Reps,
    int? DistanceMeters,
    int? DurationSeconds,
    decimal? CustomMetric,
    CreateRoutineRepRange? RepRange);

public sealed record UpdateRoutineSetWrite(
    [property: JsonConverter(typeof(SetTypeJsonConverter))] SetType Type,
    decimal? WeightKg,
    int? Reps,
    int? DistanceMeters,
    int? DurationSeconds,
    decimal? CustomMetric,
    RepRange? RepRange);

public sealed record CreateRoutineRepRange(decimal Start, decimal End);

public sealed record CreateRoutineFolderRequest(RoutineFolderWrite RoutineFolder);

public sealed record RoutineFolderWrite(string Title);
