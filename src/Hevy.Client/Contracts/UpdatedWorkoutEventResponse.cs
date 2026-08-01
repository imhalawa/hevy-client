using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record UpdatedWorkoutEventResponse([property: JsonRequired] WorkoutResponse Workout) : WorkoutEventResponse;
