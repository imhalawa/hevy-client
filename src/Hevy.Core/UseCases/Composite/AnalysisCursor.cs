namespace Hevy.Core.UseCases;

internal sealed record AnalysisCursor(
    string Endpoint,
    string Phase,
    int NextPage,
    UtcRange Range,
    int Limit,
    int PageSize,
    IReadOnlyDictionary<string, string?> Filters,
    bool IsInitial);
