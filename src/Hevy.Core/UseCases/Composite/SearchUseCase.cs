using System.Globalization;

namespace Hevy.Core.UseCases;

public sealed class SearchUseCase(
    Func<int, CancellationToken, Task<PagedResult<Routine>>> getRoutinePage,
    Func<int, CancellationToken, Task<PagedResult<ExerciseTemplate>>> getExerciseTemplatePage)
{
  public async Task<CompositeResult<RoutineSearchItem>> SearchRoutinesAsync(
      string query,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    ValidateLimit(limit);
    var filters = Filters(("query", Normalize(query)), ("limit", limit.ToString(CultureInfo.InvariantCulture)));
    return await ScanAsync(
        "routines",
        filters,
        limit,
        continuation,
        getRoutinePage,
        routine => Normalize(routine.Title).Contains(filters["query"]!, StringComparison.Ordinal),
        static routine => new RoutineSearchItem(routine.Id, CollapseWhitespace(routine.Title), routine.FolderId),
        cancellationToken).ConfigureAwait(false);
  }

  public async Task<CompositeResult<ExerciseTemplateSearchItem>> SearchExerciseTemplatesAsync(
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
    return await ScanAsync(
        "exercise-templates",
        filters,
        limit,
        continuation,
        getExerciseTemplatePage,
        template => TemplateMatches(template, filters["query"]!, normalizedEquipment, normalizedMuscle),
        static template => new ExerciseTemplateSearchItem(
            template.Id,
            CollapseWhitespace(template.Title),
            template.Type,
            template.PrimaryMuscleGroup,
            template.SecondaryMuscleGroups,
            template.EquipmentCategory,
            template.IsCustom),
        cancellationToken).ConfigureAwait(false);
  }

  private static string Normalize(string? value) => CollapseWhitespace(value).ToUpperInvariant();

  private static bool TemplateMatches(ExerciseTemplate template, string query, string? equipment, string? muscle)
  {
    var titleMatches = Normalize(template.Title).Contains(query, StringComparison.Ordinal);
    var equipmentMatches = equipment is null || string.Equals(EnumWire(template.EquipmentCategory), equipment, StringComparison.Ordinal);
    var primaryMuscleMatches = muscle is not null && string.Equals(Normalize(template.PrimaryMuscleGroup), muscle, StringComparison.Ordinal);
    var secondaryMuscleMatches = muscle is not null && template.SecondaryMuscleGroups.Any(group => string.Equals(Normalize(group), muscle, StringComparison.Ordinal));
    return titleMatches && equipmentMatches && (muscle is null || primaryMuscleMatches || secondaryMuscleMatches);
  }

  private static string CollapseWhitespace(string? value) =>
      string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

  private static async Task<CompositeResult<TResult>> ScanAsync<TSource, TResult>(
      string endpoint,
      IReadOnlyDictionary<string, string?> filters,
      int limit,
      string? continuation,
      Func<int, CancellationToken, Task<PagedResult<TSource>>> readPage,
      Func<TSource, bool> matches,
      Func<TSource, TResult> project,
      CancellationToken cancellationToken)
      where TSource : class
      where TResult : class
  {
    var state = continuation is null
        ? new ContinuationState(endpoint, 1, filters, Continuation.MaximumItemBudget)
        : Continuation.Parse(continuation, endpoint, filters);
    var sourceOffset = state.NextPage - 1;
    var page = sourceOffset / 10 + 1;
    var skip = sourceOffset % 10;
    var scanned = 0;
    var complete = false;
    var results = new List<TResult>(limit);
    var expectedPageCount = -1;

    while (results.Count < limit && scanned < Continuation.MaximumItemBudget)
    {
      var result = await readPage(page, cancellationToken).ConfigureAwait(false);
      var hasConsistentPagination = result.Page == page && result.PageCount >= 0 &&
          (expectedPageCount < 0 || result.PageCount == expectedPageCount);
      if (!hasConsistentPagination)
      {
        throw new InvalidOperationException("Hevy returned inconsistent catalog pagination.");
      }
      expectedPageCount = result.PageCount;
      var hasPossiblePage = page <= Math.Max(1, result.PageCount) &&
          result.Items.Count <= 10 &&
          (result.PageCount == 0 || result.Items.Count > 0);
      if (!hasPossiblePage)
      {
        throw new InvalidOperationException("Hevy returned an impossible catalog page.");
      }

      var processedOnPage = 0;
      foreach (var item in result.Items.Skip(skip))
      {
        processedOnPage++;
        scanned++;
        sourceOffset = checked(sourceOffset + 1);
        if (matches(item)) results.Add(project(item));
        if (results.Count == limit || scanned == Continuation.MaximumItemBudget) break;
      }

      var consumedPage = skip + processedOnPage >= result.Items.Count;
      if (sourceOffset >= (long)result.PageCount * 10 || (page == result.PageCount && consumedPage))
      {
        complete = true;
        break;
      }
      if (!consumedPage) break;
      sourceOffset = checked(page * 10);
      page++;
      skip = 0;
    }

    var more = !complete;
    var next = more
        ? Continuation.Create(endpoint, checked(sourceOffset + 1), filters, Continuation.MaximumItemBudget)
        : null;
    return new CompositeResult<TResult>([.. results], filters, limit, more, next);
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
