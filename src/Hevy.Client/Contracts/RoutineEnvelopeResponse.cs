using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record RoutineEnvelopeResponse([property: JsonRequired] RoutineResponse Routine);
