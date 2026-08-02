namespace Hevy.Client.Models;

public sealed record UpdateRoutineRequest(UpdateRoutineWriteRequest Routine)
{
  public static implicit operator UpdateRoutineRequest(UpdateRoutineCommand value) => new(UpdateRoutineWriteRequest.From(value.Routine));
}
