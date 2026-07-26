namespace TestSupport;

public static class Fixture
{
  public static string Read(string fileName)
  {
    var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
    return File.ReadAllText(path);
  }
}
