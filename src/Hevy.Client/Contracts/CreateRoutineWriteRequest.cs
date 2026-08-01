using System.Collections.Immutable;

namespace Hevy.Client.Contracts;

public sealed record CreateRoutineWriteRequest(string Title, long? FolderId, string Notes, ImmutableList<CreateRoutineExerciseWriteRequest> Exercises);
