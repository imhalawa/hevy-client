using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record UpdateBodyMeasurementRequest(decimal? WeightKg, decimal? LeanMassKg, decimal? FatPercent, decimal? NeckCm, decimal? ShoulderCm, decimal? ChestCm, decimal? LeftBicepCm, decimal? RightBicepCm, decimal? LeftForearmCm, decimal? RightForearmCm, decimal? Abdomen, decimal? Waist, decimal? Hips, decimal? LeftThigh, decimal? RightThigh, decimal? LeftCalf, decimal? RightCalf)
{
	public static implicit operator UpdateBodyMeasurementRequest(BodyMeasurementUpdate value)
	{
		return value.ToRequest();
	}

	public static implicit operator BodyMeasurementUpdate(UpdateBodyMeasurementRequest value)
	{
		return value.ToCommand();
	}
}
