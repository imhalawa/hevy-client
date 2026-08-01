using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Diagnostics;
using Microsoft.Extensions.Primitives;

namespace Hevy.Mcp.Hosting;

internal static class HttpHost
{
  internal static async Task RunAsync(string[] args, HevyMcpOptions options, CancellationToken cancellationToken)
  {

    var app = BuildApplication(args, options);
    await app.RunAsync(cancellationToken);
  }

  private static WebApplication BuildApplication(string[] args, HevyMcpOptions options)
  {
    var authToken = options.McpAuthToken ??
        throw new InvalidOperationException("MCP_AUTH_TOKEN is required for HTTP transport.");
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();
    var diagnostics = RedactingLoggerProvider.Create(options, Console.Error);
    if (diagnostics is not null)
    {
      builder.Logging.AddProvider(diagnostics);
    }

    var allowedHosts = ParseAllowedHosts(builder.Configuration["AllowedHosts"]);
    builder.Services.AddHostFiltering(hostOptions =>
    {
      hostOptions.AllowedHosts = allowedHosts;
      hostOptions.AllowEmptyHosts = false;
      hostOptions.IncludeFailureMessage = false;
    });
    builder.Services.AddHevyMcpServer(options, diagnostics).WithHttpTransport(transportOptions =>
        transportOptions.Stateless = true);

    var app = builder.Build();
    app.UseHostFiltering();
    app.Use((context, next) => AuthorizeMcpRequestAsync(context, next, authToken));
    app.MapGet("/healthz", static context =>
    {
      context.Response.StatusCode = StatusCodes.Status200OK;
      return Task.CompletedTask;
    });
    app.MapMcp("/mcp");
    return app;
  }

  private static async Task AuthorizeMcpRequestAsync(HttpContext context, RequestDelegate next, string authToken)
  {
    if (!context.Request.Path.StartsWithSegments("/mcp"))
    {
      await next(context);
      return;
    }

    if (!HasSafeOrigin(context.Request.Headers.Origin, context.Request.Host))
    {
      context.Response.StatusCode = StatusCodes.Status403Forbidden;
      return;
    }

    if (!HasValidBearerToken(context.Request.Headers.Authorization, authToken))
    {
      context.Response.StatusCode = StatusCodes.Status401Unauthorized;
      context.Response.Headers.WWWAuthenticate = "Bearer";
      return;
    }

    await next(context);
  }

  private static bool HasSafeOrigin(StringValues originValues, HostString requestHost)
  {
    if (StringValues.IsNullOrEmpty(originValues))
    {
      return true;
    }

    if (originValues.Count != 1 ||
        !Uri.TryCreate(originValues[0], UriKind.Absolute, out var origin) ||
        (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps) ||
        !string.IsNullOrEmpty(origin.UserInfo) ||
        origin.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(origin.Query) ||
        !string.IsNullOrEmpty(origin.Fragment) ||
        (origin.Scheme == Uri.UriSchemeHttp && !IsLoopbackHost(requestHost.Host)))
    {
      return false;
    }

    return string.Equals(origin.Authority, requestHost.Value, StringComparison.OrdinalIgnoreCase);
  }

  private static bool HasValidBearerToken(StringValues authorizationValues, string expectedToken)
  {
    if (authorizationValues.Count != 1 ||
        !AuthenticationHeaderValue.TryParse(authorizationValues[0], out var authorization) ||
        !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
        authorization.Parameter is not { } suppliedToken ||
        !BearerToken.IsValidToken68(suppliedToken))
    {
      return false;
    }

    var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedToken));
    var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedToken));
    return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
  }

  private static List<string> ParseAllowedHosts(string? configuredHosts)
  {
    var hosts = (configuredHosts ?? "localhost;127.0.0.1;[::1]")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();
    if (hosts.Count == 0 || hosts.Any(static host => host.Contains('*', StringComparison.Ordinal) || host.Contains('+', StringComparison.Ordinal)))
    {
      throw new InvalidOperationException("AllowedHosts must contain explicit trusted host names.");
    }

    return hosts;
  }

  private static bool IsLoopbackHost(string host) =>
      string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
      (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
}
