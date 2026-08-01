using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record UpdatedWorkoutEventResponse([property: JsonRequired] WorkoutResponse Workout) : WorkoutEventResponse;
