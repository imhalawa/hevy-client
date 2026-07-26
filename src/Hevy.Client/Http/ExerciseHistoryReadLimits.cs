namespace Hevy.Client.Http;

internal sealed record ExerciseHistoryReadLimits(long MaximumResponseBytes)
{
  internal const long DefaultMaximumResponseBytes = 16 * 1024 * 1024;
  internal static ExerciseHistoryReadLimits Default { get; } = new(DefaultMaximumResponseBytes);

  internal void Validate()
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(MaximumResponseBytes, 1);
  }
}
