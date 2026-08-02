using System.Net;

namespace Hevy.Core.Exceptions;

public sealed class HevyException : Exception
{
  public HevyException(string code, string message, bool isRetryable, HttpStatusCode? statusCode, string? requestId = null)
      : base(message)
  {
    Code = code;
    IsRetryable = isRetryable;
    StatusCode = statusCode;
    RequestId = requestId;
  }

  public string Code { get; }

  public bool IsRetryable { get; }

  public HttpStatusCode? StatusCode { get; }

  public string? RequestId { get; }
}
