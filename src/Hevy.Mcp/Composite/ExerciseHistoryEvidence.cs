using System.Globalization;
using Hevy.Client;
using Hevy.Core.Models;

namespace Hevy.Mcp.Composite;

internal sealed record ExerciseHistoryEvidence(
    string WorkoutId,
    DateTimeOffset WorkoutStartTime,
    decimal? VolumeKgReps);
