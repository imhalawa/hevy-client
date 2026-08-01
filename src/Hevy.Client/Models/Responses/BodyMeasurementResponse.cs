using System;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record BodyMeasurementResponse([property: JsonRequired] DateOnly Date, decimal? WeightKg, decimal? LeanMassKg, decimal? FatPercent, decimal? NeckCm, decimal? ShoulderCm, decimal? ChestCm, decimal? LeftBicepCm, decimal? RightBicepCm, decimal? LeftForearmCm, decimal? RightForearmCm, decimal? Abdomen, decimal? Waist, decimal? Hips, decimal? LeftThigh, decimal? RightThigh, decimal? LeftCalf, decimal? RightCalf)
{
  internal BodyMeasurement ToDomain() => new(Date, WeightKg, LeanMassKg, FatPercent, NeckCm, ShoulderCm, ChestCm, LeftBicepCm, RightBicepCm, LeftForearmCm, RightForearmCm, Abdomen, Waist, Hips, LeftThigh, RightThigh, LeftCalf, RightCalf);
}
