namespace Hevy.Core.Exceptions;

public sealed class HevyCommittedReadbackException : Exception
{
  public HevyCommittedReadbackException()
      : base("The Hevy mutation was committed, but its result could not be read back. Fetch the current state; do not replay the mutation.")
  {
  }

  public string Code => "committed_readback_failed";

  public bool IsRetryable => false;
}
