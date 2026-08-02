namespace Hevy.Core.UseCases;

public sealed record CompositeResult<T>(
    ImmutableList<T> Items,
    IReadOnlyDictionary<string, string?> Filters,
    int Limit,
    bool Truncated,
    string? Continuation)
    where T : class;
