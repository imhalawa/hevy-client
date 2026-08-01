using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record ExerciseTemplatePageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<ExerciseTemplateResponse> ExerciseTemplates);
