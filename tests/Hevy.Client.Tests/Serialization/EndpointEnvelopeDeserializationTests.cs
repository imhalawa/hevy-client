using System.Text.Json;
using Hevy.Client.Models;
using Hevy.Client.Serialization;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests.Serialization;

public sealed class EndpointEnvelopeDeserializationTests
{
  [Fact]
  public void Workout_page_deserializes_pagination_and_items()
  {
    var page = JsonSerializer.Deserialize(Fixture.Read("workout-page.json"), HevyJsonContext.Default.WorkoutPageResponse);

    (page!.PageCount).Should().Be(2);
    (page.Workouts[0].Id).Should().Be("workout-page-1");
  }

  [Fact]
  public void Workout_count_deserializes_named_count()
  {
    var count = JsonSerializer.Deserialize(Fixture.Read("workout-count.json"), HevyJsonContext.Default.WorkoutCountResponse);

    (count!.WorkoutCount).Should().Be(42);
  }

  [Fact]
  public void Routine_page_deserializes_pagination_and_items()
  {
    var page = JsonSerializer.Deserialize(Fixture.Read("routine-page.json"), HevyJsonContext.Default.RoutinePageResponse);

    (page!.PageCount).Should().Be(3);
    (page.Routines[0].Id).Should().Be("routine-page-1");
  }

  [Fact]
  public void Routine_response_deserializes_routine_envelope()
  {
    var response = JsonSerializer.Deserialize(Fixture.Read("routine-response.json"), HevyJsonContext.Default.RoutineEnvelopeResponse);

    (response!.Routine.Id).Should().Be("routine-response-1");
  }

  [Fact]
  public void Exercise_template_page_deserializes_catalog_items()
  {
    var page = JsonSerializer.Deserialize(Fixture.Read("exercise-template-page.json"), HevyJsonContext.Default.ExerciseTemplatePageResponse);

    (page!.PageCount).Should().Be(1);
    (page.ExerciseTemplates[0].Id).Should().Be("D04AC939");
  }

  [Fact]
  public void Create_exercise_template_response_deserializes_identifier()
  {
    var response = JsonSerializer.Deserialize(Fixture.Read("exercise-template-create.json"), HevyJsonContext.Default.CreateExerciseTemplateResponse);

    (response!.Id).Should().Be(123);
  }

  [Fact]
  public void Routine_folder_page_deserializes_collection()
  {
    var page = JsonSerializer.Deserialize(Fixture.Read("routine-folder-page.json"), HevyJsonContext.Default.RoutineFolderPageResponse);

    (page!.Page).Should().Be(2);
    (page.RoutineFolders[0].Id).Should().Be(42);
  }

  [Fact]
  public void Exercise_history_response_deserializes_collection()
  {
    var response = JsonSerializer.Deserialize(Fixture.Read("exercise-history-response.json"), HevyJsonContext.Default.ExerciseHistoryResponse);

    (response!.ExerciseHistory[0].WorkoutId).Should().Be("workout-history-1");
  }

  [Fact]
  public void Body_measurement_page_deserializes_collection()
  {
    var page = JsonSerializer.Deserialize(Fixture.Read("body-measurement-page.json"), HevyJsonContext.Default.BodyMeasurementPageResponse);

    (page!.Page).Should().Be(1);
    (page.BodyMeasurements[0].Date).Should().Be(new DateOnly(2024, 8, 14));
  }
}
