using System.Net;

namespace Hevy.Client.Errors;

public sealed class HevyOutcomeUnknownException : Exception
{
  public HevyOutcomeUnknownException(HttpStatusCode? statusCode = null)
      : base("The outcome of the Hevy mutation is unknown. Read the current state before attempting another write.")
  {
    StatusCode = statusCode;
  }

  public string Code => "outcome_unknown";

  public HttpStatusCode? StatusCode { get; }
}
