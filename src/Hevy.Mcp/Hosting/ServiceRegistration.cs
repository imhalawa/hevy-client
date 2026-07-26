using Hevy.Client;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Caching;
using Hevy.Mcp.Composite;
using Hevy.Mcp.Diagnostics;
using Hevy.Mcp.Prompts;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Reflection;

namespace Hevy.Mcp.Hosting;

internal static class ServiceRegistration
{
  internal static IMcpServerBuilder AddHevyMcpServer(
      this IServiceCollection services,
      HevyMcpOptions options,
      RedactingLoggerProvider? diagnostics = null)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(options);

    services.AddSingleton(options);
    services.AddSingleton(DiagnosticSnapshot.Create(options));
    services.AddSingleton(HevyClientOptions.FromEnvironment());
    services.AddSingleton<IHevyClient>(serviceProvider =>
        new HevyClient(serviceProvider.GetRequiredService<HevyClientOptions>()));
    services.AddMemoryCache(memory => memory.SizeLimit = 2);
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<HevyCache>();
    services.AddSingleton<SearchService>();
    services.AddSingleton<TrainingAnalysisService>();

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
      typeof(CompositeTools),
      typeof(DiagnosticTools),
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
    foreach (var tool in tools)
    {
      tool.ProtocolTool.InputSchema = ToolSchemas.NormalizeWireValues(tool.ProtocolTool.InputSchema);
      if (tool.ProtocolTool.OutputSchema is { } outputSchema)
      {
        tool.ProtocolTool.OutputSchema = ToolSchemas.NormalizeWireValues(outputSchema);
      }
    }
    builder.WithPrompts<HevyPrompts>(ToolResults.JsonOptions);
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
        return await DiagnosticToolDispatch.InvokeAsync(
            _ => Task.FromResult(ToolExceptionFilter.Validation($"Unknown tool '{name}'.")),
            DiagnosticOperationCategory.Protocol,
            diagnostics,
            cancellationToken);
      }

      var category = Category(name);
      return await DiagnosticToolDispatch.InvokeAsync(async invocationCancellationToken =>
        {
          request.MatchedPrimitive = tool;
          try
          {
            var result = await tool.InvokeAsync(request, invocationCancellationToken);
            return result.IsError == true && result.StructuredContent is null
                ? ToolExceptionFilter.Validation("Tool arguments did not match the advertised input schema.")
                : result;
          }
          catch (OperationCanceledException)
          {
            throw;
          }
          catch (Exception exception)
          {
            return InvocationFailure(exception);
          }
        },
        category,
        diagnostics,
        cancellationToken);
    });
  }

  internal static CallToolResult InvocationFailure(Exception exception)
  {
    ArgumentNullException.ThrowIfNull(exception);
    return exception is System.Text.Json.JsonException
        ? ToolExceptionFilter.Validation("Tool arguments did not match the advertised input schema.")
        : ToolExceptionFilter.Unexpected();
  }

  private static DiagnosticOperationCategory Category(string? toolName) => toolName switch
  {
    "get_diagnostics" => DiagnosticOperationCategory.Diagnostics,
    "search_routines" or "search_exercise_templates" or "get_workout_evidence" or
        "summarize_training" or "summarize_exercise_history" => DiagnosticOperationCategory.Composite,
    not null when toolName.StartsWith("create_", StringComparison.Ordinal) ||
        toolName.StartsWith("update_", StringComparison.Ordinal) => DiagnosticOperationCategory.Mutation,
    _ => DiagnosticOperationCategory.Read,
  };
}
