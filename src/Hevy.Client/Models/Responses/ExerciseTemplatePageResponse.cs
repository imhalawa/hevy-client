using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hevy.Client.Models;

public sealed record ExerciseTemplatePageResponse(int Page, int PageCount, [property: JsonRequired] ImmutableList<ExerciseTemplateResponse> ExerciseTemplates) : IHevyResponse
{
  public void Validate()
  {
    foreach (var template in ExerciseTemplates)
    {
      if (template is null) throw new JsonException();
      template.Validate();
    }
  }
}
