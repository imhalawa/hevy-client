using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record CreateRoutineRequest(CreateRoutineWriteRequest Routine)
{
	public static implicit operator CreateRoutineRequest(CreateRoutineCommand value)
	{
		return new CreateRoutineRequest(value.Routine.ToRequest());
	}

	public static implicit operator CreateRoutineCommand(CreateRoutineRequest value)
	{
		return value.ToCommand();
	}
}
