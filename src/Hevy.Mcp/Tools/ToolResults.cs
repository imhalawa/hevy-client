using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using Hevy.Client;
using Hevy.Core.Models;
using Hevy.Mcp.Caching;

namespace Hevy.Mcp.Tools;

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

  internal static PagedResult<T> LocalPage<T>(ImmutableList<T> catalog, int page, int pageSize)
  {
    var pageCount = catalog.Count == 0 ? 0 : (catalog.Count + pageSize - 1) / pageSize;
    if ((pageCount == 0 && page != 1) || (pageCount > 0 && page > pageCount))
    {
      throw new ArgumentOutOfRangeException(nameof(page), "page cannot exceed the cached catalog page count.");
    }
    var skip = (long)(page - 1) * pageSize;
    var items = skip > int.MaxValue ? [] : catalog.Skip((int)skip).Take(pageSize).ToImmutableList();
    return new PagedResult<T>(page, pageCount, items);
  }

  internal static void ValidatePagination(int page, int pageSize, int maximumPageSize = 10)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
    ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, maximumPageSize);
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

  internal static ExerciseHistoryPageMeta ExerciseHistoryPageMeta(
      string exerciseTemplateId,
      int page,
      int pageSize,
      DateOnly? startDate,
      DateOnly? endDate,
      string detail,
      int scannedItemCount,
      bool truncated,
      string? truncationReason) => new(page, pageSize, detail, scannedItemCount, truncated, truncationReason,
          truncated && truncationReason is null ? new ExerciseHistoryContinuation(exerciseTemplateId, page + 1, pageSize, startDate, endDate, detail) : null);

  internal static MutationMeta DryRunMeta(bool force = false, DateTimeOffset? expectedUpdatedAt = null) =>
      new(true, force, expectedUpdatedAt, []);

  internal static MutationData<TPayload, TResult> DryRunData<TPayload, TResult>(TPayload payload)
      where TPayload : class
      where TResult : class => new(Payload: payload);

  internal static MutationData<TPayload, TResult> MutationResult<TPayload, TResult>(TResult result)
      where TPayload : class
      where TResult : class => new(Result: result);
}
