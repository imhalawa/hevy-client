using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hevy.Mcp.Tests.Hosting;

[Collection("Environment variables")]
public sealed class HttpHostTests
{
  [Fact]
  public async Task HealthCheckIsAnEmptyAnonymous200Response()
  {
    await using var server = new HttpServerFixture();
    using var client = server.CreateClient();

    using var response = await client.GetAsync("/healthz");

    (response.StatusCode).Should().Be(HttpStatusCode.OK);
    (await response.Content.ReadAsStringAsync()).Should().Be(string.Empty);
  }

  [Fact]
  public async Task McpEndpointRejectsMissingMalformedAndWrongBearerCredentials()
  {
    await using var server = new HttpServerFixture();
    using var client = server.CreateClient();
    using var missing = InitializeRequest();
    using var malformed = InitializeRequest();
    malformed.Headers.Authorization = new AuthenticationHeaderValue("Basic", "not-bearer");
    using var wrong = InitializeRequest();
    wrong.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");
    using var multiple = InitializeRequest();
    multiple.Headers.TryAddWithoutValidation("Authorization", ["Bearer mcp-auth-token", "Bearer mcp-auth-token"]);

    using var missingResponse = await client.SendAsync(missing);
    using var malformedResponse = await client.SendAsync(malformed);
    using var wrongResponse = await client.SendAsync(wrong);
    using var multipleResponse = await client.SendAsync(multiple);

    (missingResponse.StatusCode).Should().Be(HttpStatusCode.Unauthorized);
    (malformedResponse.StatusCode).Should().Be(HttpStatusCode.Unauthorized);
    (wrongResponse.StatusCode).Should().Be(HttpStatusCode.Unauthorized);
    (multipleResponse.StatusCode).Should().Be(HttpStatusCode.Unauthorized);
    (missingResponse.Headers.WwwAuthenticate.Single().Scheme).Should().Be("Bearer");
  }

  [Theory]
  [InlineData("GET")]
  [InlineData("DELETE")]
  public async Task EveryHttpMethodOnTheMcpEndpointRequiresBearerAuthentication(string method)
  {
    await using var server = new HttpServerFixture();
    using var client = server.CreateClient();
    using var request = new HttpRequestMessage(new HttpMethod(method), "/mcp");

    using var response = await client.SendAsync(request);

    (response.StatusCode).Should().Be(HttpStatusCode.Unauthorized);
    (response.Headers.WwwAuthenticate.Single().Scheme).Should().Be("Bearer");
  }

  [Fact]
  public async Task McpSubpathsAreProtectedAndRejectedByMcpRouting()
  {
    await using var server = new HttpServerFixture();
    using var client = server.CreateClient();
    using var missingCredential = new HttpRequestMessage(HttpMethod.Get, "/mcp/subpath");
    using var authenticated = new HttpRequestMessage(HttpMethod.Get, "/mcp/subpath");
    authenticated.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "mcp-auth-token");

    using var protectedResponse = await client.SendAsync(missingCredential);
    using var routedResponse = await client.SendAsync(authenticated);

    (protectedResponse.StatusCode).Should().Be(HttpStatusCode.Unauthorized);
    (routedResponse.StatusCode).Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task SimilarNonMcpPathIsNotSubjectToMcpAuthentication()
  {
    await using var server = new HttpServerFixture();
    using var client = server.CreateClient();

    using var response = await client.GetAsync("/mcpx");

    (response.StatusCode).Should().Be(HttpStatusCode.NotFound);
    (response.Headers.WwwAuthenticate).Should().BeEmpty();
  }

  [Fact]
  public async Task CorrectBearerCredentialReachesAStatelessMcpServer()
  {
    await using var server = new HttpServerFixture();
    using var client = server.CreateClient();
    using var request = InitializeRequest("mcp-auth-token");

    using var response = await client.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();

    (response.StatusCode).Should().Be(HttpStatusCode.OK);
    (body).Should().Contain("\"jsonrpc\":\"2.0\"");
    (body).Should().Contain("\"id\":1");
    (body).Should().Contain("\"name\":\"hevy-mcp\"");
    (response.Headers.Contains("MCP-Session-Id")).Should().BeFalse();
  }

  [Fact]
  public async Task HostFilteringRejectsAnUnconfiguredHostBeforeMcpDispatch()
  {
    await using var server = new HttpServerFixture();
    using var client = server.CreateClient();
    using var request = InitializeRequest("mcp-auth-token");
    request.Headers.Host = "attacker.example";

    using var response = await client.SendAsync(request);

    (response.StatusCode).Should().Be(HttpStatusCode.BadRequest);
  }

  [Theory]
  [InlineData("https://attacker.example")]
  [InlineData("null")]
  [InlineData("not a URI")]
  public async Task OriginValidationRejectsUnsafeBrowserOrigins(string origin)
  {
    await using var server = new HttpServerFixture();
    using var client = server.CreateClient();
    using var request = InitializeRequest("mcp-auth-token");
    request.Headers.TryAddWithoutValidation("Origin", origin);

    using var response = await client.SendAsync(request);

    (response.StatusCode).Should().Be(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task ConfiguredReverseProxyHostAndMatchingHttpsOriginAreAccepted()
  {
    await using var server = new HttpServerFixture("proxy.example");
    using var client = server.CreateClient();
    using var request = InitializeRequest("mcp-auth-token");
    request.Headers.Host = "proxy.example";
    request.Headers.TryAddWithoutValidation("Origin", "https://proxy.example");

    using var response = await client.SendAsync(request);

    (response.StatusCode).Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task ConfiguredReverseProxyHostRejectsAnInsecureBrowserOrigin()
  {
    await using var server = new HttpServerFixture("proxy.example");
    using var client = server.CreateClient();
    using var request = InitializeRequest("mcp-auth-token");
    request.Headers.Host = "proxy.example";
    request.Headers.TryAddWithoutValidation("Origin", "http://proxy.example");

    using var response = await client.SendAsync(request);

    (response.StatusCode).Should().Be(HttpStatusCode.Forbidden);
  }

  [Fact]
  public void HttpApplicationCannotStartWithWildcardAllowedHosts()
  {
    using var environment = new HttpEnvironment("mcp-auth-token", "*.example.com");
    using var factory = new WebApplicationFactory<Program>();

    var standardError = CaptureStartupFailure(factory, out var exception);

    (exception.Message).Should().Contain("exited without ever building an IHost");
    (standardError).Should().Contain("AllowedHosts");
  }

  [Fact]
  public void HttpApplicationCannotStartWithoutAuthenticationToken()
  {
    using var environment = new HttpEnvironment(token: null, allowedHosts: "localhost");
    using var factory = new WebApplicationFactory<Program>();

    var standardError = CaptureStartupFailure(factory, out var exception);

    (exception.Message).Should().Contain("exited without ever building an IHost");
    (standardError).Should().Contain("MCP_AUTH_TOKEN");
  }

  private static string CaptureStartupFailure(WebApplicationFactory<Program> factory, out Exception exception)
  {
    using var standardError = new StringWriter();
    var originalStandardError = Console.Error;
    try
    {
      Console.SetError(standardError);
      exception = FluentActions.Invoking(factory.CreateClient).Should().Throw<Exception>().Which;
      return standardError.ToString();
    }
    finally
    {
      Console.SetError(originalStandardError);
    }
  }

  private static HttpRequestMessage InitializeRequest(string? bearerToken = null)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
    {
      Content = new StringContent(
          """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"http-test","version":"1.0"}}}""",
          Encoding.UTF8,
          "application/json"),
    };
    request.Headers.Accept.ParseAdd("application/json, text/event-stream");
    if (bearerToken is not null)
    {
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    return request;
  }

  private sealed class HttpServerFixture : IAsyncDisposable
  {
    private readonly HttpEnvironment environment;
    private readonly WebApplicationFactory<Program> factory = new();

    internal HttpServerFixture(string allowedHosts = "localhost") =>
        environment = new HttpEnvironment("mcp-auth-token", allowedHosts);

    internal HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false,
      BaseAddress = new Uri("http://localhost"),
    });

    public async ValueTask DisposeAsync()
    {
      await factory.DisposeAsync();
      environment.Dispose();
    }
  }

  private sealed class HttpEnvironment : IDisposable
  {
    private static readonly string[] Names = ["HEVY_API_KEY", "HEVY_MCP_TRANSPORT", "MCP_AUTH_TOKEN", "HEVY_READ_ONLY", "AllowedHosts"];
    private readonly Dictionary<string, string?> originalValues = [];

    internal HttpEnvironment(string? token, string allowedHosts)
    {
      foreach (var name in Names)
      {
        originalValues[name] = Environment.GetEnvironmentVariable(name);
      }

      Environment.SetEnvironmentVariable("HEVY_API_KEY", "http-test-api-key");
      Environment.SetEnvironmentVariable("HEVY_MCP_TRANSPORT", "http");
      Environment.SetEnvironmentVariable("MCP_AUTH_TOKEN", token);
      Environment.SetEnvironmentVariable("HEVY_READ_ONLY", null);
      Environment.SetEnvironmentVariable("AllowedHosts", allowedHosts);
    }

    public void Dispose()
    {
      foreach (var (name, value) in originalValues)
      {
        Environment.SetEnvironmentVariable(name, value);
      }
    }
  }
}

[CollectionDefinition("Environment variables", DisableParallelization = true)]
public sealed class EnvironmentVariableCollection
{
}
