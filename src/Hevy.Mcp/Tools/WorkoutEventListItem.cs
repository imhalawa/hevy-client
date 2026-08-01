using Hevy.Core.Models;

namespace Hevy.Mcp.Tools;

internal sealed record WorkoutEventListItem(
    string Type,
    string Id,
    DateTimeOffset? UpdatedAt = null,
    DateTimeOffset? DeletedAt = null,
    Workout? Workout = null);
