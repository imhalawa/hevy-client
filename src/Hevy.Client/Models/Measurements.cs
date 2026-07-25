namespace Hevy.Client.Models;

public sealed record BodyMeasurement(
    DateOnly Date,
    decimal? WeightKg,
    decimal? LeanMassKg,
    decimal? FatPercent,
    decimal? NeckCm,
    decimal? ShoulderCm,
    decimal? ChestCm,
    decimal? LeftBicepCm,
    decimal? RightBicepCm,
    decimal? LeftForearmCm,
    decimal? RightForearmCm,
    decimal? Abdomen,
    decimal? Waist,
    decimal? Hips,
    decimal? LeftThigh,
    decimal? RightThigh,
    decimal? LeftCalf,
    decimal? RightCalf);

public sealed record BodyMeasurementPage(int Page, int PageCount, IReadOnlyList<BodyMeasurement> BodyMeasurements);

public sealed record CreateBodyMeasurementRequest(
    DateOnly Date,
    decimal? WeightKg,
    decimal? LeanMassKg,
    decimal? FatPercent,
    decimal? NeckCm,
    decimal? ShoulderCm,
    decimal? ChestCm,
    decimal? LeftBicepCm,
    decimal? RightBicepCm,
    decimal? LeftForearmCm,
    decimal? RightForearmCm,
    decimal? Abdomen,
    decimal? Waist,
    decimal? Hips,
    decimal? LeftThigh,
    decimal? RightThigh,
    decimal? LeftCalf,
    decimal? RightCalf);

public sealed record UpdateBodyMeasurementRequest(
    decimal? WeightKg,
    decimal? LeanMassKg,
    decimal? FatPercent,
    decimal? NeckCm,
    decimal? ShoulderCm,
    decimal? ChestCm,
    decimal? LeftBicepCm,
    decimal? RightBicepCm,
    decimal? LeftForearmCm,
    decimal? RightForearmCm,
    decimal? Abdomen,
    decimal? Waist,
    decimal? Hips,
    decimal? LeftThigh,
    decimal? RightThigh,
    decimal? LeftCalf,
    decimal? RightCalf);
