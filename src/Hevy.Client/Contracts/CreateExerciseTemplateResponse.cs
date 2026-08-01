using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record CreateExerciseTemplateResponse([property: JsonRequired] int Id);
