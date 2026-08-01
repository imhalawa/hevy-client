using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record RoutineEnvelopeResponse([property: JsonRequired] RoutineResponse Routine) : IHevyResponse
{
  public void Validate() => Routine.Validate();
}
