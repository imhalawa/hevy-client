using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hevy.Core.Models;
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
    Validate(request.Date, request.WeightKg, request.LeanMassKg, request.FatPercent, request.NeckCm, request.ShoulderCm, request.ChestCm, request.LeftBicepCm, request.RightBicepCm, request.LeftForearmCm, request.RightForearmCm, request.Abdomen, request.Waist, request.Hips, request.LeftThigh, request.RightThigh, request.LeftCalf, request.RightCalf);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<CreateBodyMeasurementRequest, BodyMeasurement>(request), "Body-measurement payload is valid; no request was sent.", ToolResults.DryRunMeta());
    var result = await ToolResults.Client(services).CreateBodyMeasurementAsync(request, cancellationToken);
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
    Validate(date, request.WeightKg, request.LeanMassKg, request.FatPercent, request.NeckCm, request.ShoulderCm, request.ChestCm, request.LeftBicepCm, request.RightBicepCm, request.LeftForearmCm, request.RightForearmCm, request.Abdomen, request.Waist, request.Hips, request.LeftThigh, request.RightThigh, request.LeftCalf, request.RightCalf);
    ToolValidation.Guard(expected_updated_at, force);
    var guardLimitation = "Hevy body measurements do not expose updated_at; writes require explicit force after reviewing current state.";
    var dryRunMeta = new MutationMeta(true, force, expected_updated_at, [], GuardAvailable: false, GuardLimitation: guardLimitation);
    if (dry_run) return ToolResults.Success(ToolResults.DryRunData<UpdateBodyMeasurementRequest, BodyMeasurement>(request), "Body-measurement replacement payload is valid; no request was sent.", dryRunMeta);
    var client = ToolResults.Client(services);
    if (!force)
    {
      await client.GetBodyMeasurementAsync(date, cancellationToken);
      return ToolExceptionFilter.Conflict(
          "Hevy body measurements do not expose updated_at, so the guard cannot be verified; retry only with force after reviewing the current measurement.",
          new MutationMeta(false, false, expected_updated_at, GuardAvailable: false, GuardLimitation: guardLimitation));
    }
    var result = await client.UpdateBodyMeasurementAsync(date, request, cancellationToken);
    return ToolResults.Success(ToolResults.MutationResult<UpdateBodyMeasurementRequest, BodyMeasurement>(result), $"Updated body measurement for {result.Date:yyyy-MM-dd}.", new MutationMeta(false, true, expected_updated_at, GuardAvailable: false, GuardLimitation: guardLimitation));
  });

  private static void Validate(DateOnly date, params decimal?[] values) => ToolValidation.Measurement(date, values);
}
