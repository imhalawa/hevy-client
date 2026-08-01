using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record WorkoutCountResponse([property: JsonRequired] int WorkoutCount);
