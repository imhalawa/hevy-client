using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hevy.Client.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class MeasurementReadTools
{
  [McpServerTool(Name = "get_body_measurements", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get one page of body measurements. Dates are calendar dates; weight is kilograms and circumference is centimeters.")]
  internal static Task<CallToolResult> GetBodyMeasurements(IServiceProvider services, [Range(1, int.MaxValue)] int page = 1, [Range(1, 10)] int page_size = 10, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ToolResults.ValidatePagination(page, page_size);
    ToolResults.ValidateDetail(detail);
    var result = await ToolResults.Client(services).GetBodyMeasurementsAsync(page, page_size, cancellationToken);
    return ToolResults.Success(new { items = result.Items }, $"Returned {result.Items.Count} body measurements.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
  });

  [McpServerTool(Name = "get_body_measurement", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Get body measurements for one calendar date; weight is kilograms and circumference is centimeters.")]
  internal static Task<CallToolResult> GetBodyMeasurement(IServiceProvider services, DateOnly date, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    if (date == DateOnly.MinValue) throw new ArgumentException("date is required.", nameof(date));
    var item = await ToolResults.Client(services).GetBodyMeasurementAsync(date, cancellationToken);
    return ToolResults.Success(item, $"Returned body measurement for {date:yyyy-MM-dd}.");
  });
}

internal static class MeasurementWriteTools
{
  [McpServerTool(Name = "create_body_measurement", Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Create body measurements for one calendar date; weight is kilograms and circumference is centimeters.")]
  internal static Task<CallToolResult> CreateBodyMeasurement(IServiceProvider services, CreateBodyMeasurementRequest request, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    Validate(request.Date, request.WeightKg, request.LeanMassKg, request.FatPercent, request.NeckCm, request.ShoulderCm, request.ChestCm, request.LeftBicepCm, request.RightBicepCm, request.LeftForearmCm, request.RightForearmCm, request.Abdomen, request.Waist, request.Hips, request.LeftThigh, request.RightThigh, request.LeftCalf, request.RightCalf);
    if (dry_run) return ToolResults.Success(request, "Body-measurement payload is valid; no request was sent.", ToolResults.DryRunMeta());
    var result = await ToolResults.Client(services).CreateBodyMeasurementAsync(request, cancellationToken);
    return ToolResults.Success(result, $"Created body measurement for {result.Date:yyyy-MM-dd}.", new { dry_run = false });
  });

  [McpServerTool(Name = "update_body_measurement", Destructive = true, Idempotent = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolResultEnvelope))]
  [Description("Replace body measurements for one calendar date; explicitly use force because Hevy does not expose updated_at for measurements.")]
  internal static Task<CallToolResult> UpdateBodyMeasurement(IServiceProvider services, DateOnly date, UpdateBodyMeasurementRequest request, DateTimeOffset? expected_updated_at = null, bool force = false, bool dry_run = false, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    ArgumentNullException.ThrowIfNull(request);
    Validate(date, request.WeightKg, request.LeanMassKg, request.FatPercent, request.NeckCm, request.ShoulderCm, request.ChestCm, request.LeftBicepCm, request.RightBicepCm, request.LeftForearmCm, request.RightForearmCm, request.Abdomen, request.Waist, request.Hips, request.LeftThigh, request.RightThigh, request.LeftCalf, request.RightCalf);
    ToolValidation.Guard(expected_updated_at, force);
    if (dry_run) return ToolResults.Success(request, "Body-measurement replacement payload is valid; no request was sent.", ToolResults.DryRunMeta(force, expected_updated_at));
    var client = ToolResults.Client(services);
    if (!force)
    {
      await client.GetBodyMeasurementAsync(date, cancellationToken);
      return ToolExceptionFilter.Conflict("Hevy body measurements do not expose updated_at, so the guard cannot be verified; retry only with force after reviewing the current measurement.");
    }
    var result = await client.UpdateBodyMeasurementAsync(date, request, cancellationToken);
    return ToolResults.Success(result, $"Updated body measurement for {result.Date:yyyy-MM-dd}.", new { dry_run = false, forced = true, expected_updated_at });
  });

  private static void Validate(DateOnly date, params decimal?[] values) => ToolValidation.Measurement(date, values);
}
