using Hevy.Client;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Reflection;

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

    var builder = services.AddMcpServer(serverOptions => serverOptions.ServerInfo = new Implementation
    {
      Name = "hevy-client",
      Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    });

    var toolTypes = new List<Type>
    {
      typeof(WorkoutReadTools),
      typeof(RoutineReadTools),
      typeof(ExerciseReadTools),
      typeof(MeasurementReadTools),
      typeof(UserTools),
    };

    if (!options.ReadOnly)
    {
      toolTypes.AddRange([
        typeof(WorkoutWriteTools),
        typeof(RoutineWriteTools),
        typeof(ExerciseWriteTools),
        typeof(MeasurementWriteTools),
      ]);
    }

    var tools = toolTypes
        .SelectMany(static type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        .Where(static method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
        .Select(static method => McpServerTool.Create(method, target: null, new McpServerToolCreateOptions
        {
          SerializerOptions = ToolResults.JsonOptions,
        }))
        .OrderBy(static tool => tool.ProtocolTool.Name, StringComparer.Ordinal)
        .ToArray();
    builder.WithListToolsHandler((request, _) =>
    {
      var protocolTools = tools.Select(static tool => tool.ProtocolTool).ToList();
      return ValueTask.FromResult(new ListToolsResult { Tools = protocolTools });
    });
    return builder.WithCallToolHandler(async (request, cancellationToken) =>
    {
      var name = request.Params?.Name;
      var tool = tools.SingleOrDefault(candidate => string.Equals(candidate.ProtocolTool.Name, name, StringComparison.Ordinal));
      if (tool is null)
      {
        return ToolExceptionFilter.Validation($"Unknown tool '{name}'.");
      }

      request.MatchedPrimitive = tool;
      return await tool.InvokeAsync(request, cancellationToken);
    });
  }
}
