using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record Workout(
    string Id,
    string Title,
    string RoutineId,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset UpdatedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<WorkoutExercise> Exercises);

public sealed record WorkoutExercise(
    int Index,
    string Title,
    string Notes,
    string ExerciseTemplateId,
    [property: JsonPropertyName("supersets_id")] long? SupersetId,
    IReadOnlyList<WorkoutSet> Sets);

public sealed record WorkoutSet(
    int Index,
    string Type,
    decimal? WeightKg,
    decimal? Reps,
    decimal? DistanceMeters,
    decimal? DurationSeconds,
    decimal? Rpe,
    decimal? CustomMetric)
    : SetMetrics(WeightKg, Reps, DistanceMeters, DurationSeconds, Rpe, CustomMetric);

public sealed record WorkoutPage(int Page, int PageCount, IReadOnlyList<Workout> Workouts);

public sealed record WorkoutCountResponse(int WorkoutCount);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UpdatedWorkoutEvent), "updated")]
[JsonDerivedType(typeof(DeletedWorkoutEvent), "deleted")]
public abstract record WorkoutEvent;

public sealed record UpdatedWorkoutEvent(Workout Workout) : WorkoutEvent;

public sealed record DeletedWorkoutEvent(string Id, DateTimeOffset DeletedAt) : WorkoutEvent;

public sealed record WorkoutEventsPage(int Page, int PageCount, IReadOnlyList<WorkoutEvent> Events);

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
