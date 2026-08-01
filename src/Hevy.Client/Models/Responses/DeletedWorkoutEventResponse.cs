using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record DeletedWorkoutEventResponse([property: JsonRequired] string Id, [property: JsonRequired] DateTimeOffset DeletedAt) : WorkoutEventResponse
{
  public override void Validate()
  {
    if (string.IsNullOrWhiteSpace(Id) || DeletedAt == default) throw new JsonException();
  }

  internal override WorkoutEvent ToDomain() => new DeletedWorkoutEvent(Id, DeletedAt);
}
