using Hevy.Core.Models;

namespace Hevy.Client.Models;

public sealed record CreateRoutineRequest(CreateRoutineWriteRequest Routine)
{
  public static implicit operator CreateRoutineRequest(CreateRoutineCommand value) => new(CreateRoutineWriteRequest.From(value.Routine));

  public static implicit operator CreateRoutineCommand(CreateRoutineRequest value) => new(value.Routine.ToDomain());
}
