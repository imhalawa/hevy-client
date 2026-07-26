using Hevy.Client;
using Hevy.Mcp.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Hevy.Mcp.Hosting;

internal static class ServiceRegistration
{
  internal static IMcpServerBuilder AddHevyMcpServer(this IServiceCollection services, HevyMcpOptions options)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(options);

    services.AddSingleton(options);
    services.AddSingleton(HevyClientOptions.FromEnvironment());
    services.AddSingleton<IHevyClient>(serviceProvider =>
        new HevyClient(serviceProvider.GetRequiredService<HevyClientOptions>()));

    return services.AddMcpServer(serverOptions => serverOptions.ServerInfo = new Implementation
    {
      Name = "hevy-client",
      Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    }).WithListToolsHandler((_, _) => ValueTask.FromResult(new ListToolsResult
    {
      Tools = [],
    }));
  }
}
