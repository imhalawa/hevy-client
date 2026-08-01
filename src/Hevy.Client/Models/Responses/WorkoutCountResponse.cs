using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record WorkoutCountResponse([property: JsonRequired] int WorkoutCount);
