namespace Hevy.Client.Models;

public sealed record CreateRoutineRequest(CreateRoutineWriteRequest Routine)
{
  public static implicit operator CreateRoutineRequest(CreateRoutineCommand value) => new(CreateRoutineWriteRequest.From(value.Routine));
}
