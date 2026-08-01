using Hevy.Core.Models;

namespace Hevy.Mcp.Tools;

internal sealed record MutationMeta(
    bool DryRun,
    bool Forced = false,
    DateTimeOffset? ExpectedUpdatedAt = null,
    ImmutableList<string>? ValidationWarnings = null,
    bool GuardAvailable = true,
    string? GuardLimitation = null);
