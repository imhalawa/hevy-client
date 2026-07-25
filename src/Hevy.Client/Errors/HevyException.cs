using System.Net;

namespace Hevy.Client.Errors;

public sealed class HevyException : Exception
{
  public HevyException(string code, string message, bool isRetryable, HttpStatusCode? statusCode)
      : base(message)
  {
    Code = code;
    IsRetryable = isRetryable;
    StatusCode = statusCode;
  }

  public string Code { get; }

  public bool IsRetryable { get; }

  public HttpStatusCode? StatusCode { get; }
}
