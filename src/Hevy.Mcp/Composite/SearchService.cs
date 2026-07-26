using System.Globalization;
using Hevy.Client.Models;
using Hevy.Mcp.Caching;

namespace Hevy.Mcp.Composite;

internal sealed record CompositeResult<T>(
    IReadOnlyList<T> Items,
    IReadOnlyDictionary<string, string?> Filters,
    int Limit,
    bool Truncated,
    string? Continuation)
    where T : class;

internal sealed record RoutineSearchItem(string Id, string Title, long? FolderId);

internal sealed record ExerciseTemplateSearchItem(
    string Id,
    string Title,
    string Type,
    string PrimaryMuscleGroup,
    IReadOnlyList<string> SecondaryMuscleGroups,
    EquipmentCategory EquipmentCategory,
    bool IsCustom);

internal sealed class SearchService(HevyCache cache)
{
  internal async Task<CompositeResult<RoutineSearchItem>> SearchRoutinesAsync(
      string query,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    ValidateLimit(limit);
    var filters = Filters(("query", Normalize(query)), ("limit", limit.ToString(CultureInfo.InvariantCulture)));
    var catalog = await cache.GetRoutinesAsync(cancellationToken).ConfigureAwait(false);
    var matches = catalog
        .Where(routine => Normalize(routine.Title).Contains(filters["query"]!, StringComparison.Ordinal))
        .Select(static routine => new RoutineSearchItem(routine.Id, CollapseWhitespace(routine.Title), routine.FolderId))
        .OrderBy(static routine => routine.Title, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static routine => routine.Id, StringComparer.Ordinal)
        .ToArray();
    return Page("routines", matches, filters, limit, continuation);
  }

  internal async Task<CompositeResult<ExerciseTemplateSearchItem>> SearchExerciseTemplatesAsync(
      string query,
      string? equipment,
      string? muscle,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    ValidateLimit(limit);
    var normalizedEquipment = NormalizeOptionalEquipment(equipment);
    var normalizedMuscle = NormalizeOptional(muscle);
    var filters = Filters(
        ("equipment", normalizedEquipment),
        ("limit", limit.ToString(CultureInfo.InvariantCulture)),
        ("muscle", normalizedMuscle),
        ("query", Normalize(query)));
    var catalog = await cache.GetExerciseTemplatesAsync(cancellationToken).ConfigureAwait(false);
    var matches = catalog
        .Where(template => Normalize(template.Title).Contains(filters["query"]!, StringComparison.Ordinal))
        .Where(template => normalizedEquipment is null || string.Equals(EnumWire(template.EquipmentCategory), normalizedEquipment, StringComparison.Ordinal))
        .Where(template => normalizedMuscle is null ||
            string.Equals(Normalize(template.PrimaryMuscleGroup), normalizedMuscle, StringComparison.Ordinal) ||
            template.SecondaryMuscleGroups.Any(group => string.Equals(Normalize(group), normalizedMuscle, StringComparison.Ordinal)))
        .Select(static template => new ExerciseTemplateSearchItem(
            template.Id,
            CollapseWhitespace(template.Title),
            template.Type,
            template.PrimaryMuscleGroup,
            template.SecondaryMuscleGroups,
            template.EquipmentCategory,
            template.IsCustom))
        .OrderBy(static template => template.Title, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static template => template.Id, StringComparer.Ordinal)
        .ToArray();
    return Page("exercise-templates", matches, filters, limit, continuation);
  }

  internal static string Normalize(string? value) => CollapseWhitespace(value).ToUpperInvariant();

  internal static string CollapseWhitespace(string? value) =>
      string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

  private static CompositeResult<T> Page<T>(
      string endpoint,
      IReadOnlyList<T> matches,
      IReadOnlyDictionary<string, string?> filters,
      int limit,
      string? continuation)
      where T : class
  {
    var state = continuation is null
        ? new ContinuationState(endpoint, 1, filters, Continuation.MaximumItemBudget)
        : Continuation.Parse(continuation, endpoint, filters);
    var take = Math.Min(limit, state.RemainingItemBudget);
    var offset = checked((state.NextPage - 1) * limit);
    if (offset > matches.Count)
    {
      throw new ArgumentException("The continuation points beyond the available search results.", nameof(continuation));
    }

    var items = matches.Skip(offset).Take(take).ToArray();
    var more = offset + items.Length < matches.Count;
    var remaining = state.RemainingItemBudget - items.Length;
    var next = more && remaining > 0
        ? Continuation.Create(endpoint, state.NextPage + 1, filters, remaining)
        : null;
    return new CompositeResult<T>(items, filters, limit, more, next);
  }

  private static IReadOnlyDictionary<string, string?> Filters(params (string Key, string? Value)[] filters) =>
      new SortedDictionary<string, string?>(filters.ToDictionary(static filter => filter.Key, static filter => filter.Value, StringComparer.Ordinal), StringComparer.Ordinal);

  private static string? NormalizeOptional(string? value)
  {
    var normalized = Normalize(value);
    return normalized.Length == 0 ? null : normalized;
  }

  private static string? NormalizeOptionalEquipment(string? value)
  {
    var normalized = CollapseWhitespace(value).ToLowerInvariant().Replace(' ', '_');
    if (normalized.Length == 0) return null;
    if (!Enum.GetValues<EquipmentCategory>().Select(EnumWire).Contains(normalized, StringComparer.Ordinal))
    {
      throw new ArgumentException("equipment must be a documented Hevy equipment category.", nameof(value));
    }
    return normalized;
  }

  private static string EnumWire(EquipmentCategory category) => category switch
  {
    EquipmentCategory.ResistanceBand => "resistance_band",
    _ => category.ToString().ToLowerInvariant(),
  };

  private static void ValidateLimit(int limit)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, Continuation.MaximumItemBudget);
  }
}
