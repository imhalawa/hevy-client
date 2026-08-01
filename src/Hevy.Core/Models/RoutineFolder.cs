using System;

namespace Hevy.Core.Models;

public sealed record RoutineFolder(long Id, int Index, string Title, DateTimeOffset UpdatedAt, DateTimeOffset CreatedAt);
