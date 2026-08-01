using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record UpdateRoutineRequest(UpdateRoutineWriteRequest Routine)
{
	public static implicit operator UpdateRoutineRequest(UpdateRoutineCommand value)
	{
		return new UpdateRoutineRequest(value.Routine.ToRequest());
	}

	public static implicit operator UpdateRoutineCommand(UpdateRoutineRequest value)
	{
		return value.ToCommand();
	}
}
