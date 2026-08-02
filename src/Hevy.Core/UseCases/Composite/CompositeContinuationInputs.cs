namespace Hevy.Core.UseCases;

public sealed record CompositeContinuationInputs(
    int Weeks,
    DateTimeOffset RangeEndUtc,
    int Limit,
    string Continuation);
