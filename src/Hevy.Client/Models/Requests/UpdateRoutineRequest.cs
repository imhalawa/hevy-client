using Hevy.Core.Models;

namespace Hevy.Client.Models;

public sealed record UpdateRoutineRequest(UpdateRoutineWriteRequest Routine)
{
  public static implicit operator UpdateRoutineRequest(UpdateRoutineCommand value) => new(UpdateRoutineWriteRequest.From(value.Routine));

  public static implicit operator UpdateRoutineCommand(UpdateRoutineRequest value) => new(value.Routine.ToDomain());
}
