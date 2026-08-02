namespace Hevy.Client.Models;

public sealed record CreateRoutineFolderRequest(RoutineFolderWrite RoutineFolder)
{
  public static implicit operator CreateRoutineFolderRequest(CreateRoutineFolderCommand value) => new(value.RoutineFolder);
}
