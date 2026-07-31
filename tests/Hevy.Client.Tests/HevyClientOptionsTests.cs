using Hevy.Client;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyClientOptionsTests
{
  [Fact]
  public void Environment_configuration_is_not_exposed_by_the_client_options()
  {
    (typeof(HevyClientOptions).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)).Should().NotContain((method => method.ReturnType == typeof(HevyClientOptions)));
  }

  [Fact]
  public void Raw_api_key_construction_is_not_public()
  {
    (typeof(HevyClientOptions).GetConstructors()
        .Any(constructor => constructor.GetParameters() is [{ ParameterType: { } parameterType }] && parameterType == typeof(string))).Should().BeFalse();
  }
}
