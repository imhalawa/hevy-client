namespace Hevy.Core.UseCases;

public sealed record RoutineFolderWrite(string Title)
{
  public void Validate()
  {
    if (string.IsNullOrWhiteSpace(Title)) throw new ArgumentException("A routine folder title is required.", nameof(Title));
  }
}
