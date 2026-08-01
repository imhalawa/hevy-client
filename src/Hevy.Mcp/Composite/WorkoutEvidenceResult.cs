using System.Globalization;
using Hevy.Client;
using Hevy.Core.Models;

namespace Hevy.Mcp.Composite;

internal sealed record WorkoutEvidenceResult(
    ImmutableList<WorkoutEvidenceItem> Items,
    int Weeks,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    bool Truncated,
    string? Continuation,
    CompositeContinuationInputs? ContinuationInputs);
