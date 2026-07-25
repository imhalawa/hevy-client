using Hevy.Client;
using Xunit;

namespace Hevy.Client.Tests;

[Collection("Environment variables")]
public sealed class HevyClientOptionsTests
{
  // Break caught: production configuration reading a credential from any source other than HEVY_API_KEY.
  [Fact]
  public void From_environment_reads_the_hevy_api_key()
  {
    var original = Environment.GetEnvironmentVariable("HEVY_API_KEY");
    try
    {
      Environment.SetEnvironmentVariable("HEVY_API_KEY", "environment-api-key");

      var options = HevyClientOptions.FromEnvironment();

      Assert.Equal("environment-api-key", options.ApiKey);
    }
    finally
    {
      Environment.SetEnvironmentVariable("HEVY_API_KEY", original);
    }
  }

  // Break caught: production startup continuing without the only approved credential source.
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void From_environment_rejects_an_absent_or_blank_api_key(string? value)
  {
    var original = Environment.GetEnvironmentVariable("HEVY_API_KEY");
    try
    {
      Environment.SetEnvironmentVariable("HEVY_API_KEY", value);

      var exception = Assert.Throws<InvalidOperationException>(HevyClientOptions.FromEnvironment);

      Assert.Equal("HEVY_API_KEY is required.", exception.Message);
    }
    finally
    {
      Environment.SetEnvironmentVariable("HEVY_API_KEY", original);
    }
  }

  // Break caught: callers bypassing the environment-only production credential boundary through a public raw-key constructor.
  [Fact]
  public void Raw_api_key_construction_is_not_public()
  {
    Assert.DoesNotContain(
        typeof(HevyClientOptions).GetConstructors(),
        constructor => constructor.GetParameters() is [{ ParameterType: { } parameterType }] && parameterType == typeof(string));
  }
}

[CollectionDefinition("Environment variables", DisableParallelization = true)]
public sealed class EnvironmentVariablesCollection;
