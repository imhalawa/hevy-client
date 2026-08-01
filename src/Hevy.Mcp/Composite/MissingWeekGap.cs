using System.Globalization;
using Hevy.Client;
using Hevy.Core.Models;

namespace Hevy.Mcp.Composite;

internal sealed record MissingWeekGap(DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc);
