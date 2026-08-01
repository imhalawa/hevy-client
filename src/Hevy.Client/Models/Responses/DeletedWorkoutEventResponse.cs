using System;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record DeletedWorkoutEventResponse([property: JsonRequired] string Id, [property: JsonRequired] DateTimeOffset DeletedAt) : WorkoutEventResponse;
