namespace Hevy.Core.UseCases;

public sealed record MissingWeekGap(DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc);
