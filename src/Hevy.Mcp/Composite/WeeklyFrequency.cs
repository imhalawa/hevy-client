using System.Globalization;
using Hevy.Client;
using Hevy.Core.Models;

namespace Hevy.Mcp.Composite;

internal sealed record WeeklyFrequency(
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    int ChunkWorkoutCount,
    ImmutableList<WorkoutEvidenceReference> Evidence);
