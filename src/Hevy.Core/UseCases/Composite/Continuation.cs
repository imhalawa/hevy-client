using System.Buffers.Text;
using System.Text.Json;

namespace Hevy.Core.UseCases;

internal static class Continuation
{
  internal const int MaximumItemBudget = 1_000;
  private static readonly string[] AllowedProperties = ["endpoint", "filters", "next_page", "remaining_item_budget"];

  internal static string Create(
      string endpoint,
      int nextPage,
      IReadOnlyDictionary<string, string?> filters,
      int remainingItemBudget)
  {
    Validate(endpoint, nextPage, remainingItemBudget);
    var payload = new
    {
      endpoint,
      filters = CanonicalFilters(filters),
      next_page = nextPage,
      remaining_item_budget = remainingItemBudget,
    };
    return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload));
  }

  internal static ContinuationState Parse(
      string token,
      string expectedEndpoint,
      IReadOnlyDictionary<string, string?> expectedFilters)
  {
    var state = Parse(token, expectedEndpoint);
    if (!FiltersEqual(state.Filters, expectedFilters))
    {
      throw new ArgumentException("The continuation does not match this endpoint and its original filters.", nameof(token));
    }
    return state;
  }

  internal static ContinuationState Parse(string token, string expectedEndpoint)
  {
    if (string.IsNullOrWhiteSpace(token) || token.Length > 8_192)
    {
      throw new ArgumentException("The continuation is malformed.", nameof(token));
    }

    JsonDocument document;
    try
    {
      document = JsonDocument.Parse(Decode(token));
    }
    catch (Exception exception) when (exception is FormatException or JsonException)
    {
      throw new ArgumentException("The continuation is malformed.", nameof(token));
    }

    using (document)
    {
      var root = document.RootElement;
      if (!HasExpectedShape(root))
      {
        throw new ArgumentException("The continuation payload has unexpected fields.", nameof(token));
      }

      var state = ReadState(root, token);
      Validate(state.Endpoint, state.NextPage, state.RemainingItemBudget);
      return string.Equals(state.Endpoint, expectedEndpoint, StringComparison.Ordinal)
          ? state
          : throw new ArgumentException("The continuation does not match this endpoint and its original filters.", nameof(token));
    }
  }

  private static bool HasExpectedShape(JsonElement root) =>
      root.ValueKind == JsonValueKind.Object &&
      root.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal)
          .SequenceEqual(AllowedProperties.Order(StringComparer.Ordinal), StringComparer.Ordinal);

  private static IReadOnlyDictionary<string, string?> ReadFilters(JsonElement element)
  {
    if (element.ValueKind != JsonValueKind.Object)
    {
      throw new ArgumentException("Continuation filters must be an object.", nameof(element));
    }
    var filters = new SortedDictionary<string, string?>(StringComparer.Ordinal);
    foreach (var property in element.EnumerateObject())
    {
      if (string.IsNullOrWhiteSpace(property.Name) || property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
      {
        throw new ArgumentException("Continuation filters must contain string or null values.", nameof(element));
      }
      filters.Add(property.Name, property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString());
    }
    return filters;
  }

  private static SortedDictionary<string, string?> CanonicalFilters(IReadOnlyDictionary<string, string?> filters)
  {
    var canonical = new SortedDictionary<string, string?>(StringComparer.Ordinal);
    foreach (var (key, value) in filters)
    {
      if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Continuation filter names cannot be blank.", nameof(filters));
      canonical.Add(key, value);
    }
    return canonical;
  }

  private static bool FiltersEqual(IReadOnlyDictionary<string, string?> left, IReadOnlyDictionary<string, string?> right) =>
      left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));

  private static void Validate(string endpoint, int nextPage, int remainingItemBudget)
  {
    if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("A continuation endpoint is required.", nameof(endpoint));
    ArgumentOutOfRangeException.ThrowIfLessThan(nextPage, 1);
    ArgumentOutOfRangeException.ThrowIfLessThan(remainingItemBudget, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(remainingItemBudget, MaximumItemBudget);
  }

  private static ContinuationState ReadState(JsonElement root, string token)
  {
    try
    {
      return new ContinuationState(
          root.GetProperty("endpoint").GetString() ?? string.Empty,
          root.GetProperty("next_page").GetInt32(),
          ReadFilters(root.GetProperty("filters")),
          root.GetProperty("remaining_item_budget").GetInt32());
    }
    catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or FormatException)
    {
      throw new ArgumentException("The continuation is malformed.", nameof(token));
    }
  }

  private static byte[] Decode(string token) => Base64Url.DecodeFromChars(token);
}
