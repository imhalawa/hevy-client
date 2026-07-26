using Hevy.Client.Models;

namespace Hevy.Mcp.Tools;

internal sealed record ToolOutput<TData, TMeta>(
    bool Ok,
    TData? Data = default,
    ToolError? Error = null,
    TMeta? Meta = default)
    where TData : class
    where TMeta : class;

internal sealed record NoMeta;

internal sealed record ItemsData<T>(IReadOnlyList<T> Items) where T : class;

internal sealed record WorkoutCountData(int WorkoutCount);

internal sealed record PageMeta<TContinuation>(
    int Page,
    int PageCount,
    int PageSize,
    string Detail,
    bool Truncated,
    TContinuation? Continuation = default)
    where TContinuation : class;

internal sealed record PageContinuation(int Page, int PageSize, string Detail);

internal sealed record WorkoutEventContinuation(
    int Page,
    int PageSize,
    DateTimeOffset Since,
    string Detail);

internal sealed record ExerciseHistoryContinuation(
    string ExerciseTemplateId,
    int Page,
    int PageSize,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Detail);

internal sealed record MutationData<TPayload, TResult>(
    TPayload? Payload = default,
    TResult? Result = default)
    where TPayload : class
    where TResult : class;

internal sealed record MutationMeta(
    bool DryRun,
    bool Forced = false,
    DateTimeOffset? ExpectedUpdatedAt = null,
    IReadOnlyList<string>? ValidationWarnings = null,
    bool GuardAvailable = true,
    string? GuardLimitation = null);

internal sealed record WorkoutListItem(
    string Id,
    string Title,
    string RoutineId,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset UpdatedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<WorkoutExercise>? Exercises = null);

internal sealed record RoutineListItem(
    string Id,
    string Title,
    long? FolderId,
    DateTimeOffset UpdatedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RoutineExercise>? Exercises = null);

internal sealed record WorkoutEventListItem(
    string Type,
    string Id,
    DateTimeOffset? UpdatedAt = null,
    DateTimeOffset? DeletedAt = null,
    Workout? Workout = null);

internal sealed record ExerciseHistoryListItem(
    string WorkoutId,
    string WorkoutTitle,
    DateTimeOffset WorkoutStartTime,
    string ExerciseTemplateId,
    string SetType,
    DateTimeOffset? WorkoutEndTime = null,
    decimal? WeightKg = null,
    decimal? Reps = null,
    decimal? DistanceMeters = null,
    decimal? DurationSeconds = null,
    decimal? Rpe = null,
    decimal? CustomMetric = null);
