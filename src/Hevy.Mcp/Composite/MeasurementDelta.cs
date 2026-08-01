using System.Globalization;
using Hevy.Client;
using Hevy.Core.Models;

namespace Hevy.Mcp.Composite;

internal sealed record MeasurementDelta(
    string Metric,
    decimal FirstValue,
    decimal LastValue,
    decimal Delta,
    ImmutableList<DateOnly> EvidenceDates);
