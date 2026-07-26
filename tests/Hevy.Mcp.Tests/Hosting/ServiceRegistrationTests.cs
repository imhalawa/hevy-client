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
    Assert.Equal("validation_error", error.GetProperty("code").GetString());
    Assert.DoesNotContain("invalid untrusted argument", error.GetProperty("message").GetString(), StringComparison.Ordinal);
  }

  [Theory]
  [MemberData(nameof(InternalFaults))]
  public void InternalInvocationOrSerializationFaultReturnsSafeUnexpectedError(Exception exception)
  {
    var result = ServiceRegistration.InvocationFailure(exception);

    var error = result.Structured().GetProperty("error");
    Assert.Equal("unexpected_error", error.GetProperty("code").GetString());
    Assert.Equal("The tool could not complete the request.", error.GetProperty("message").GetString());
    Assert.DoesNotContain(exception.Message, error.GetProperty("message").GetString(), StringComparison.Ordinal);
  }

  public static TheoryData<Exception> InternalFaults => new()
  {
    new InvalidOperationException("internal invocation detail"),
    new NotSupportedException("internal serialization detail"),
  };
}
