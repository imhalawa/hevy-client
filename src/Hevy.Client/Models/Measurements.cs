using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record BodyMeasurement(
    [property: JsonRequired] DateOnly Date,
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

public sealed record BodyMeasurementPage(
    [property: JsonRequired] int Page,
    [property: JsonRequired] int PageCount,
    [property: JsonRequired] IReadOnlyList<BodyMeasurement> BodyMeasurements);

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
