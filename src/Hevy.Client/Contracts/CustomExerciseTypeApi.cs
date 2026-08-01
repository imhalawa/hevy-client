using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public enum CustomExerciseTypeApi
{
	[JsonStringEnumMemberName("weight_reps")]
	WeightReps,
	[JsonStringEnumMemberName("reps_only")]
	RepsOnly,
	[JsonStringEnumMemberName("bodyweight_reps")]
	BodyweightReps,
	[JsonStringEnumMemberName("bodyweight_assisted_reps")]
	BodyweightAssistedReps,
	[JsonStringEnumMemberName("duration")]
	Duration,
	[JsonStringEnumMemberName("weight_duration")]
	WeightDuration,
	[JsonStringEnumMemberName("distance_duration")]
	DistanceDuration,
	[JsonStringEnumMemberName("short_distance_weight")]
	ShortDistanceWeight
}
