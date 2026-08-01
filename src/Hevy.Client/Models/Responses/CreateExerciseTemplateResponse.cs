using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record CreateExerciseTemplateResponse([property: JsonRequired] int Id);
