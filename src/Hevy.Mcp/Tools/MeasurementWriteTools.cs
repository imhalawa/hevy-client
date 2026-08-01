using System.ComponentModel;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class MeasurementWriteTools
{
  [McpServerTool(Name = "create_body_measurement", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<CreateBodyMeasurementRequest, BodyMeasurement>, MutationMeta>))]
  [Description("Create body measurements for one calendar date; weight is kilograms and circumference is centimeters.")]
  internal static Task<CallToolResult> CreateBodyMeasurement(IServiceProvider services, CreateBodyMeasurementRequest request, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    NewBodyMeasurement measurement = request;
    var result = await measurement.ExecuteAsync(ToolResults.Client(services), dry_run, cancellationToken);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateBodyMeasurementRequest, BodyMeasurement>(request), "Body-measurement payload is valid; no request was sent.", ToolResults.DryRunMeta());
    ArgumentNullException.ThrowIfNull(result);
    return ToolResults.Success(ToolResults.MutationResult<CreateBodyMeasurementRequest, BodyMeasurement>(result), $"Created body measurement for {result.Date:yyyy-MM-dd}.", new MutationMeta(false));
  });

  [McpServerTool(Name = "update_body_measurement", Destructive = true, Idempotent = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<MutationData<UpdateBodyMeasurementRequest, BodyMeasurement>, MutationMeta>))]
  [Description("Replace body measurements for one calendar date; explicitly use force because Hevy does not expose updated_at for measurements.")]
  internal static Task<CallToolResult> UpdateBodyMeasurement(
      IServiceProvider services,
      DateOnly date,
      UpdateBodyMeasurementRequest request,
      [Description("Accepted for a uniform update contract, but Hevy body measurements do not expose updated_at so this guard cannot be verified.")] DateTimeOffset? expected_updated_at = null,
      [Description("Required for a body-measurement write because Hevy exposes no updated_at concurrency guard.")] bool force = false,
      bool dry_run = false,
      CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    var guardLimitation = "Hevy body measurements do not expose updated_at; writes require explicit force after reviewing current state.";
    var dryRunMeta = new MutationMeta(true, force, expected_updated_at, [], GuardAvailable: false, GuardLimitation: guardLimitation);
    BodyMeasurementUpdate measurement = request;
    BodyMeasurement? result;
    try
    {
      result = await measurement.ExecuteAsync(ToolResults.Client(services), date, expected_updated_at, force, dry_run, cancellationToken);
    }
    catch (Hevy.Core.Exceptions.HevyConflictException exception)
    {
      return ToolExceptionFilter.Conflict(
          exception.Message,
          new MutationMeta(false, false, expected_updated_at, GuardAvailable: false, GuardLimitation: guardLimitation));
    }
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<UpdateBodyMeasurementRequest, BodyMeasurement>(request), "Body-measurement replacement payload is valid; no request was sent.", dryRunMeta);
    ArgumentNullException.ThrowIfNull(result);
    return ToolResults.Success(ToolResults.MutationResult<UpdateBodyMeasurementRequest, BodyMeasurement>(result), $"Updated body measurement for {result.Date:yyyy-MM-dd}.", new MutationMeta(false, true, expected_updated_at, GuardAvailable: false, GuardLimitation: guardLimitation));
  });
}
