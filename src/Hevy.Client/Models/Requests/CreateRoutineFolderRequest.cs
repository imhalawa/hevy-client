using Hevy.Core.Models;

namespace Hevy.Client.Models;

public sealed record CreateRoutineFolderRequest(RoutineFolderWrite RoutineFolder)
{
  public static implicit operator CreateRoutineFolderRequest(CreateRoutineFolderCommand value) => new(value.RoutineFolder);

  public static implicit operator CreateRoutineFolderCommand(CreateRoutineFolderRequest value) => new(value.RoutineFolder);
}
