using System.Collections.Immutable;

namespace Hevy.Client.Contracts;

public sealed record UpdateRoutineWriteRequest(string Title, string? Notes, ImmutableList<UpdateRoutineExerciseWriteRequest> Exercises);
