namespace Hevy.Core.UseCases;

internal sealed record ContinuationState(
    string Endpoint,
    int NextPage,
    IReadOnlyDictionary<string, string?> Filters,
    int RemainingItemBudget);
