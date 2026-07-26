using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record Workout(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string RoutineId,
    [property: JsonRequired] string Description,
    [property: JsonRequired] DateTimeOffset StartTime,
    [property: JsonRequired] DateTimeOffset EndTime,
    [property: JsonRequired] DateTimeOffset UpdatedAt,
    [property: JsonRequired] DateTimeOffset CreatedAt,
    [property: JsonRequired] IReadOnlyList<WorkoutExercise> Exercises);

public sealed record WorkoutExercise(
    [property: JsonRequired] int Index,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Notes,
    [property: JsonRequired] string ExerciseTemplateId,
    [property: JsonPropertyName("supersets_id")] long? SupersetId,
    [property: JsonRequired] IReadOnlyList<WorkoutSet> Sets);

public sealed record WorkoutSet(
    [property: JsonRequired] int Index,
    [property: JsonRequired] string Type,
    decimal? WeightKg,
    decimal? Reps,
    decimal? DistanceMeters,
    decimal? DurationSeconds,
    decimal? Rpe,
    decimal? CustomMetric)
    : SetMetrics(WeightKg, Reps, DistanceMeters, DurationSeconds, Rpe, CustomMetric);

public sealed record WorkoutPage(
    [property: JsonRequired] int Page,
    [property: JsonRequired] int PageCount,
    [property: JsonRequired] IReadOnlyList<Workout> Workouts);

public sealed record WorkoutCountResponse([property: JsonRequired] int WorkoutCount);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UpdatedWorkoutEvent), "updated")]
[JsonDerivedType(typeof(DeletedWorkoutEvent), "deleted")]
public abstract record WorkoutEvent;

public sealed record UpdatedWorkoutEvent([property: JsonRequired] Workout Workout) : WorkoutEvent;

public sealed record DeletedWorkoutEvent([property: JsonRequired] string Id, [property: JsonRequired] DateTimeOffset DeletedAt) : WorkoutEvent;

public sealed record WorkoutEventsPage(
    [property: JsonRequired] int Page,
    [property: JsonRequired] int PageCount,
    [property: JsonRequired] IReadOnlyList<WorkoutEvent> Events);

public sealed record CreateWorkoutRequest(WorkoutWrite Workout);

public sealed record UpdateWorkoutRequest(WorkoutWrite Workout);

public sealed record WorkoutWrite(
    string Title,
    string? Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool IsPrivate,
    IReadOnlyList<WorkoutExerciseWrite> Exercises);

public sealed record WorkoutExerciseWrite(
    string ExerciseTemplateId,
    long? SupersetId,
    string? Notes,
    IReadOnlyList<WorkoutSetWrite> Sets);

public sealed record WorkoutSetWrite(
    [property: JsonConverter(typeof(SetTypeJsonConverter))] SetType Type,
    decimal? WeightKg,
    int? Reps,
    int? DistanceMeters,
    int? DurationSeconds,
    decimal? CustomMetric,
    [property: JsonConverter(typeof(WorkoutRpeJsonConverter))] WorkoutRpe? Rpe);
