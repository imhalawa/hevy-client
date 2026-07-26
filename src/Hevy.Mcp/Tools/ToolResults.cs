using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using Hevy.Client;
using Hevy.Client.Models;
using Hevy.Mcp.Caching;

namespace Hevy.Mcp.Tools;

internal sealed record ToolResultEnvelope(bool Ok, object? Data = null, ToolError? Error = null, object? Meta = null);

internal sealed record ToolError(
    string Code,
    string Message,
    bool Retryable,
    string CorrelationId,
    int? HevyStatus = null,
    string? HevyRequestId = null);

internal static class ToolResults
{
  internal static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
  {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
  };

  internal static CallToolResult Success(object? data, string summary = "Hevy request completed.", object? meta = null) =>
      Create(new ToolResultEnvelope(true, data, Meta: meta), summary, isError: false);

  internal static CallToolResult Error(ToolError error, object? meta = null) =>
      Create(new ToolResultEnvelope(false, Error: error, Meta: meta), $"{error.Code}: {error.Message} (correlation_id: {error.CorrelationId})", isError: true);

  private static CallToolResult Create(ToolResultEnvelope envelope, string text, bool isError) => new()
  {
    Content = [new TextContentBlock { Text = text }],
    StructuredContent = JsonSerializer.SerializeToElement(envelope, JsonOptions),
    IsError = isError,
  };

  internal static IHevyClient Client(IServiceProvider services) =>
      services.GetService(typeof(IHevyClient)) as IHevyClient ??
      throw new InvalidOperationException("IHevyClient is unavailable.");

  internal static T Service<T>(IServiceProvider services) where T : class =>
      services.GetService(typeof(T)) as T ?? throw new InvalidOperationException($"{typeof(T).Name} is unavailable.");

  internal static HevyCache? Cache(IServiceProvider services) => services.GetService(typeof(HevyCache)) as HevyCache;

  internal static PagedResult<T> LocalPage<T>(IReadOnlyList<T> catalog, int page, int pageSize)
  {
    var pageCount = catalog.Count == 0 ? 0 : (catalog.Count + pageSize - 1) / pageSize;
    var skip = (long)(page - 1) * pageSize;
    var items = skip > int.MaxValue ? [] : catalog.Skip((int)skip).Take(pageSize).ToArray();
    return new PagedResult<T>(page, pageCount, items);
  }

  internal static void ValidatePagination(int page, int pageSize)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
    ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 10);
  }

  internal static void ValidateDetail(string detail)
  {
    if (detail is not ("compact" or "full"))
    {
      throw new ArgumentException("detail must be either 'compact' or 'full'.", nameof(detail));
    }
  }

  internal static void ValidateIdentifier(string value, string name)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new ArgumentException("An identifier is required.", name);
    }
  }

  internal static PageMeta<PageContinuation> PageMeta(int page, int pageCount, int pageSize, string detail) =>
      new(page, pageCount, pageSize, detail, page < pageCount,
          page < pageCount ? new PageContinuation(page + 1, pageSize, detail) : null);

  internal static PageMeta<WorkoutEventContinuation> WorkoutEventPageMeta(
      int page,
      int pageCount,
      int pageSize,
      DateTimeOffset since,
      string detail) => new(page, pageCount, pageSize, detail, page < pageCount,
          page < pageCount ? new WorkoutEventContinuation(page + 1, pageSize, since, detail) : null);

  internal static PageMeta<ExerciseHistoryContinuation> ExerciseHistoryPageMeta(
      string exerciseTemplateId,
      int page,
      int pageCount,
      int pageSize,
      DateOnly? startDate,
      DateOnly? endDate,
      string detail) => new(page, pageCount, pageSize, detail, page < pageCount,
          page < pageCount ? new ExerciseHistoryContinuation(exerciseTemplateId, page + 1, pageSize, startDate, endDate, detail) : null);

  internal static MutationMeta DryRunMeta(bool force = false, DateTimeOffset? expectedUpdatedAt = null) =>
      new(true, force, expectedUpdatedAt, []);

  internal static MutationData<TPayload, TResult> DryRunData<TPayload, TResult>(TPayload payload)
      where TPayload : class
      where TResult : class => new(Payload: payload);

  internal static MutationData<TPayload, TResult> MutationResult<TPayload, TResult>(TResult result)
      where TPayload : class
      where TResult : class => new(Result: result);
}

internal static class ToolValidation
{
  internal static void Workout(Hevy.Client.Models.WorkoutWrite workout)
  {
    ArgumentNullException.ThrowIfNull(workout);
    Required(workout.Title, "workout title");
    if (workout.EndTime < workout.StartTime) throw new ArgumentException("Workout end time cannot be before its start time.", nameof(workout));
    ArgumentNullException.ThrowIfNull(workout.Exercises);
    foreach (var exercise in workout.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      Required(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets) ArgumentNullException.ThrowIfNull(set);
    }
  }

  internal static void Routine(Hevy.Client.Models.CreateRoutineWrite routine)
  {
    ArgumentNullException.ThrowIfNull(routine);
    Required(routine.Title, "routine title");
    ArgumentNullException.ThrowIfNull(routine.Exercises);
    foreach (var exercise in routine.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      Required(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets) ArgumentNullException.ThrowIfNull(set);
    }
  }

  internal static void Routine(Hevy.Client.Models.UpdateRoutineWrite routine)
  {
    ArgumentNullException.ThrowIfNull(routine);
    Required(routine.Title, "routine title");
    ArgumentNullException.ThrowIfNull(routine.Exercises);
    foreach (var exercise in routine.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      Required(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets) ArgumentNullException.ThrowIfNull(set);
    }
  }

  internal static void Exercise(Hevy.Client.Models.CustomExerciseWrite exercise)
  {
    ArgumentNullException.ThrowIfNull(exercise);
    Required(exercise.Title, "exercise title");
    ArgumentNullException.ThrowIfNull(exercise.OtherMuscles);
    if (!Enum.IsDefined(exercise.ExerciseType) || !Enum.IsDefined(exercise.EquipmentCategory) ||
        !Enum.IsDefined(exercise.MuscleGroup) || exercise.OtherMuscles.Any(static muscle => !Enum.IsDefined(muscle)))
    {
      throw new ArgumentOutOfRangeException(nameof(exercise), "Exercise fields must use documented enum values.");
    }
  }

  internal static void Measurement(DateOnly date, params decimal?[] values)
  {
    if (date == DateOnly.MinValue) throw new ArgumentException("A measurement date is required.", nameof(date));
    if (values.Any(static value => value is < 0)) throw new ArgumentOutOfRangeException(nameof(values), "Measurement values cannot be negative.");
  }

  internal static void Guard(DateTimeOffset? expectedUpdatedAt, bool force)
  {
    if (!force && expectedUpdatedAt is null)
    {
      throw new ArgumentException("expected_updated_at is required unless force is true.", nameof(expectedUpdatedAt));
    }
  }

  internal static void Required(string value, string field)
  {
    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"A {field} is required.", field);
  }
}
