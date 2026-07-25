using System.Text.Json;
using Hevy.Client.Serialization;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests.Serialization;

public sealed class EndpointEnvelopeDeserializationTests
{
    // Break caught: mapping the workout list item field or page_count to a different wire name.
    [Fact]
    public void Workout_page_deserializes_pagination_and_items()
    {
        var page = JsonSerializer.Deserialize(Fixture.Read("workout-page.json"), HevyJsonContext.Default.WorkoutPage);

        Assert.Equal(2, page!.PageCount);
        Assert.Equal("workout-page-1", page.Workouts[0].Id);
    }

    // Break caught: serializing the documented workout_count response as an untyped scalar.
    [Fact]
    public void Workout_count_deserializes_named_count()
    {
        var count = JsonSerializer.Deserialize(Fixture.Read("workout-count.json"), HevyJsonContext.Default.WorkoutCountResponse);

        Assert.Equal(42, count!.WorkoutCount);
    }

    // Break caught: mapping the paginated routines collection to the wrong endpoint field.
    [Fact]
    public void Routine_page_deserializes_pagination_and_items()
    {
        var page = JsonSerializer.Deserialize(Fixture.Read("routine-page.json"), HevyJsonContext.Default.RoutinePage);

        Assert.Equal(3, page!.PageCount);
        Assert.Equal("routine-page-1", page.Routines[0].Id);
    }

    // Break caught: flattening the documented get-routine response envelope.
    [Fact]
    public void Routine_response_deserializes_routine_envelope()
    {
        var response = JsonSerializer.Deserialize(Fixture.Read("routine-response.json"), HevyJsonContext.Default.RoutineResponse);

        Assert.Equal("routine-response-1", response!.Routine.Id);
    }

    // Break caught: mapping exercise_templates to a generic items property.
    [Fact]
    public void Exercise_template_page_deserializes_catalog_items()
    {
        var page = JsonSerializer.Deserialize(Fixture.Read("exercise-template-page.json"), HevyJsonContext.Default.ExerciseTemplatePage);

        Assert.Equal(1, page!.PageCount);
        Assert.Equal("D04AC939", page.ExerciseTemplates[0].Id);
    }

    // Break caught: parsing the custom-template create identifier as a string or wrong field.
    [Fact]
    public void Create_exercise_template_response_deserializes_identifier()
    {
        var response = JsonSerializer.Deserialize(Fixture.Read("exercise-template-create.json"), HevyJsonContext.Default.CreateExerciseTemplateResponse);

        Assert.Equal(123, response!.Id);
    }

    // Break caught: mapping routine_folders to an incorrectly named list property.
    [Fact]
    public void Routine_folder_page_deserializes_collection()
    {
        var page = JsonSerializer.Deserialize(Fixture.Read("routine-folder-page.json"), HevyJsonContext.Default.RoutineFolderPage);

        Assert.Equal(2, page!.Page);
        Assert.Equal(42, page.RoutineFolders[0].Id);
    }

    // Break caught: losing the exercise_history response envelope around history entries.
    [Fact]
    public void Exercise_history_response_deserializes_collection()
    {
        var response = JsonSerializer.Deserialize(Fixture.Read("exercise-history-response.json"), HevyJsonContext.Default.ExerciseHistoryResponse);

        Assert.Equal("workout-history-1", response!.ExerciseHistory[0].WorkoutId);
    }

    // Break caught: mapping body_measurements to a wrong plural wire name.
    [Fact]
    public void Body_measurement_page_deserializes_collection()
    {
        var page = JsonSerializer.Deserialize(Fixture.Read("body-measurement-page.json"), HevyJsonContext.Default.BodyMeasurementPage);

        Assert.Equal(1, page!.Page);
        Assert.Equal(new DateOnly(2024, 8, 14), page.BodyMeasurements[0].Date);
    }
}
