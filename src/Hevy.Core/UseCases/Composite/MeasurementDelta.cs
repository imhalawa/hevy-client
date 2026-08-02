namespace Hevy.Core.UseCases;

public sealed record MeasurementDelta(
    string Metric,
    decimal FirstValue,
    decimal LastValue,
    decimal Delta,
    ImmutableList<DateOnly> EvidenceDates);
