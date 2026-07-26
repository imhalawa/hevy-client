using Hevy.Mcp.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hevy.Mcp.Hosting;

internal static class StdioHost
{
  internal static async Task RunAsync(string[] args, HevyMcpOptions options, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(args);
    ArgumentNullException.ThrowIfNull(options);

    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders();
    builder.Services.AddHevyMcpServer(options).WithStdioServerTransport();

    using var host = builder.Build();
    await host.RunAsync(cancellationToken);
  }
}
