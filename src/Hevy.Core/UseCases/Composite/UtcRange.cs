namespace Hevy.Core.UseCases;

internal sealed record UtcRange(int Weeks, DateTimeOffset Start, DateTimeOffset End);
