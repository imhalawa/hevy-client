namespace Hevy.Mcp.Configuration;

internal static class BearerToken
{
  internal static bool IsValidToken68(string? value)
  {
    if (string.IsNullOrEmpty(value))
    {
      return false;
    }

    var hasTokenCharacter = false;
    var reachedPadding = false;
    foreach (var character in value)
    {
      if (character == '=')
      {
        reachedPadding = true;
        continue;
      }

      if (reachedPadding || !IsTokenCharacter(character))
      {
        return false;
      }

      hasTokenCharacter = true;
    }

    return hasTokenCharacter;
  }

  private static bool IsTokenCharacter(char character) =>
      character is >= 'A' and <= 'Z' or
          >= 'a' and <= 'z' or
          >= '0' and <= '9' or
          '-' or '.' or '_' or '~' or '+' or '/';
}
