using System.Net;

namespace Hevy.Core.Exceptions;

public sealed class HevyOutcomeUnknownException : Exception
{
  public HevyOutcomeUnknownException(HttpStatusCode? statusCode = null, string? requestId = null)
      : base("The outcome of the Hevy mutation is unknown. Read the current state before attempting another write.")
  {
    StatusCode = statusCode;
    RequestId = requestId;
  }

  public string Code => "outcome_unknown";

  public HttpStatusCode? StatusCode { get; }

  public string? RequestId { get; }
}
