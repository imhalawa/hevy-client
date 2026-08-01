using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record ExerciseTemplatePageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<ExerciseTemplateResponse> ExerciseTemplates);
