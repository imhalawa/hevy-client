using System.Text.Json;
using Hevy.Mcp.Hosting;
using Hevy.Mcp.Tests.Tools;
using Xunit;

namespace Hevy.Mcp.Tests.Hosting;

public sealed class ServiceRegistrationTests
{
  [Fact]
  public void JsonArgumentBindingFaultReturnsValidationError()
  {
    var result = ServiceRegistration.InvocationFailure(new JsonException("invalid untrusted argument"));

    var error = result.Structured().GetProperty("error");
    (error.GetProperty("code").GetString()).Should().Be("validation_error");
    (error.GetProperty("message").GetString()).Should().NotContain("invalid untrusted argument");
  }

  [Theory]
  [MemberData(nameof(InternalFaults))]
  public void InternalInvocationOrSerializationFaultReturnsSafeUnexpectedError(Exception exception)
  {
    var result = ServiceRegistration.InvocationFailure(exception);

    var error = result.Structured().GetProperty("error");
    (error.GetProperty("code").GetString()).Should().Be("unexpected_error");
    (error.GetProperty("message").GetString()).Should().Be("The tool could not complete the request.");
    (error.GetProperty("message").GetString()).Should().NotContain(exception.Message);
  }

  public static TheoryData<Exception> InternalFaults => new()
  {
    new InvalidOperationException("internal invocation detail"),
    new NotSupportedException("internal serialization detail"),
  };
}
