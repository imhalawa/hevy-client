using System.Collections.Immutable;

namespace Hevy.Client.Contracts;

public sealed record UpdateRoutineExerciseWriteRequest(string ExerciseTemplateId, long? SupersetId, int? RestSeconds, string? Notes, ImmutableList<UpdateRoutineSetWriteRequest> Sets);
