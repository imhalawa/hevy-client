using System;
using System.Collections.Immutable;

namespace Hevy.Client.Contracts;

public sealed record WorkoutWriteRequest(string Title, string? Description, DateTimeOffset StartTime, DateTimeOffset EndTime, bool IsPrivate, ImmutableList<WorkoutExerciseWriteRequest> Exercises);
