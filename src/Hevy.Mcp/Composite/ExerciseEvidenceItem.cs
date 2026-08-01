using System.Globalization;
using Hevy.Client;
using Hevy.Core.Models;

namespace Hevy.Mcp.Composite;

internal sealed record ExerciseEvidenceItem(
    string ExerciseTemplateId,
    string Title,
    decimal VolumeKgReps,
    int CountedSets);
