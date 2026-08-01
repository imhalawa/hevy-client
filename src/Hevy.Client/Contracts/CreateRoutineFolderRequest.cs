using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record CreateRoutineFolderRequest(RoutineFolderWrite RoutineFolder)
{
	public static implicit operator CreateRoutineFolderRequest(CreateRoutineFolderCommand value)
	{
		return new CreateRoutineFolderRequest(value.RoutineFolder);
	}

	public static implicit operator CreateRoutineFolderCommand(CreateRoutineFolderRequest value)
	{
		return value.ToCommand();
	}
}
