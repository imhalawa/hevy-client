using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hevy.Mcp.Tools;

internal static class MeasurementReadTools
{
  [McpServerTool(Name = "get_body_measurements", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<ItemsData<BodyMeasurement>, PageMeta<PageContinuation>>))]
  [Description("Get one page of body measurements. Dates are calendar dates; weight is kilograms and circumference is centimeters.")]
  internal static Task<CallToolResult> GetBodyMeasurements(IServiceProvider services, [Range(1, int.MaxValue)] int page = 1, [Range(1, 10)] int page_size = 10, [RegularExpression("^(compact|full)$")] string detail = "compact", CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    new PageRequest(page, page_size, 10, detail).Validate();
    var result = await ToolResults.Client(services).GetBodyMeasurementsAsync(page, page_size, cancellationToken);
    return ToolResults.Success(new { items = result.Items }, $"Returned {result.Items.Count} body measurements.", ToolResults.PageMeta(result.Page, result.PageCount, page_size, detail));
  });

  [McpServerTool(Name = "get_body_measurement", ReadOnly = true, OpenWorld = true, UseStructuredContent = true, OutputSchemaType = typeof(ToolOutput<BodyMeasurement, NoMeta>))]
  [Description("Get body measurements for one calendar date; weight is kilograms and circumference is centimeters.")]
  internal static Task<CallToolResult> GetBodyMeasurement(IServiceProvider services, DateOnly date, CancellationToken cancellationToken = default) => ToolExceptionFilter.ExecuteAsync(async () =>
  {
    if (date == DateOnly.MinValue) throw new ArgumentException("date is required.", nameof(date));
    var item = await ToolResults.Client(services).GetBodyMeasurementAsync(date, cancellationToken);
    return ToolResults.Success(item, $"Returned body measurement for {date:yyyy-MM-dd}.");
  });
}
