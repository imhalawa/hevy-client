using System.Text.Json;
using Hevy.Core.Models;
using Hevy.Client.Models;
using Hevy.Client.Serialization;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests.Serialization;

public sealed class RequestSerializationTests
{
  [Fact]
  public void Workout_set_write_rejects_unknown_set_type()
  {
    var set = new WorkoutSetWriteRequest((SetTypeApi)999, null, null, null, null, null, new WorkoutRpe(8m));

    FluentActions.Invoking(() => JsonSerializer.Serialize(set, HevyJsonContext.Default.WorkoutSetWriteRequest)).Should().ThrowExactly<JsonException>();
  }

  [Fact]
  public void Workout_rpe_rejects_undocumented_value()
  {
    FluentActions.Invoking(() => new WorkoutRpe(8.25m)).Should().ThrowExactly<ArgumentOutOfRangeException>();
  }

  [Fact]
  public void Workout_rpe_default_value_fails_safely_during_serialization()
  {
    var set = new CreateWorkoutSetWrite(SetType.Normal, null, null, null, null, null, default(WorkoutRpe));

    FluentActions.Invoking(() => WorkoutSetWriteRequest.From(set)).Should().ThrowExactly<JsonException>();
  }

  [Fact]
  public void Create_workout_serializes_only_writable_snake_case_fields()
  {
    var json = JsonSerializer.Serialize(FixtureFactory.CreateWorkoutCommand(), HevyJsonContext.Default.CreateWorkoutRequest);

    AssertJsonTreeEqual(
        """
            {"workout":{"title":"Friday Leg Day","description":"Sanitized workout","start_time":"2024-08-14T12:00:00+00:00","end_time":"2024-08-14T12:30:00+00:00","is_private":false,"exercises":[{"exercise_template_id":"D04AC939","superset_id":null,"notes":"Sanitized note","sets":[{"type":"normal","weight_kg":100,"reps":10,"distance_meters":null,"duration_seconds":null,"custom_metric":null,"rpe":8.5}]}]}}
            """,
        json);
  }

  [Fact]
  public void Update_workout_preserves_workout_envelope()
  {
    var json = JsonSerializer.Serialize(FixtureFactory.UpdateWorkoutCommand(), HevyJsonContext.Default.UpdateWorkoutRequest);

    AssertJsonTreeEqual(
        """
            {"workout":{"title":"Friday Leg Day","description":"Sanitized workout","start_time":"2024-08-14T12:00:00+00:00","end_time":"2024-08-14T12:30:00+00:00","is_private":false,"exercises":[{"exercise_template_id":"D04AC939","superset_id":null,"notes":"Sanitized note","sets":[{"type":"normal","weight_kg":100,"reps":10,"distance_meters":null,"duration_seconds":null,"custom_metric":null,"rpe":8.5}]}]}}
            """,
        json);
  }

  [Fact]
  public void Create_routine_serializes_documented_fields()
  {
    var json = JsonSerializer.Serialize(FixtureFactory.CreateRoutineCommand(), HevyJsonContext.Default.CreateRoutineRequest);

    AssertJsonTreeEqual(
        """
            {"routine":{"title":"April Leg Day","folder_id":null,"notes":"Sanitized routine","exercises":[{"exercise_template_id":"D04AC939","superset_id":null,"rest_seconds":90,"notes":"Controlled","sets":[{"type":"normal","weight_kg":100,"reps":10,"distance_meters":null,"duration_seconds":null,"custom_metric":null,"rep_range":{"start":8,"end":12}}]}]}}
            """,
        json);
  }

  [Fact]
  public void Update_routine_omits_create_only_folder_field()
  {
    var json = JsonSerializer.Serialize(FixtureFactory.UpdateRoutineCommand(), HevyJsonContext.Default.UpdateRoutineRequest);

    AssertJsonTreeEqual(
        """
            {"routine":{"title":"April Leg Day","notes":"Sanitized routine","exercises":[{"exercise_template_id":"D04AC939","superset_id":null,"rest_seconds":90,"notes":"Controlled","sets":[{"type":"normal","weight_kg":100,"reps":10,"distance_meters":null,"duration_seconds":null,"custom_metric":null,"rep_range":{"start":8,"end":12}}]}]}}
            """,
        json);
  }

  [Fact]
  public void Create_routine_folder_serializes_routine_folder_envelope()
  {
    var json = JsonSerializer.Serialize(FixtureFactory.CreateRoutineFolderCommand(), HevyJsonContext.Default.CreateRoutineFolderRequest);

    AssertJsonTreeEqual("""{"routine_folder":{"title":"Push Pull"}}""", json);
  }

  [Fact]
  public void Create_exercise_template_serializes_enum_wire_values()
  {
    var json = JsonSerializer.Serialize(FixtureFactory.CreateExerciseTemplateCommand(), HevyJsonContext.Default.CreateExerciseTemplateRequest);

    AssertJsonTreeEqual(
        """
            {"exercise":{"title":"Bench Press","exercise_type":"weight_reps","equipment_category":"barbell","muscle_group":"chest","other_muscles":["triceps","shoulders"]}}
            """,
        json);
  }

  [Fact]
  public void Create_body_measurement_serializes_date_and_metrics()
  {
    var json = JsonSerializer.Serialize(FixtureFactory.NewBodyMeasurement(), HevyJsonContext.Default.CreateBodyMeasurementRequest);

    AssertJsonTreeEqual(
        """
            {"date":"2024-08-14","weight_kg":80.5,"lean_mass_kg":65,"fat_percent":18.5,"neck_cm":38,"shoulder_cm":115,"chest_cm":95,"left_bicep_cm":35,"right_bicep_cm":35.5,"left_forearm_cm":28,"right_forearm_cm":28.5,"abdomen":85,"waist":80,"hips":95,"left_thigh":55,"right_thigh":55.5,"left_calf":37,"right_calf":37.5}
            """,
        json);
  }

  [Fact]
  public void Update_body_measurement_omits_path_owned_date()
  {
    var json = JsonSerializer.Serialize(FixtureFactory.BodyMeasurementUpdate(), HevyJsonContext.Default.UpdateBodyMeasurementRequest);

    AssertJsonTreeEqual(
        """
            {"weight_kg":80.5,"lean_mass_kg":65,"fat_percent":18.5,"neck_cm":38,"shoulder_cm":115,"chest_cm":95,"left_bicep_cm":35,"right_bicep_cm":35.5,"left_forearm_cm":28,"right_forearm_cm":28.5,"abdomen":85,"waist":80,"hips":95,"left_thigh":55,"right_thigh":55.5,"left_calf":37,"right_calf":37.5}
            """,
        json);
  }

  private static void AssertJsonTreeEqual(string expectedJson, string actualJson)
  {
    using var expected = JsonDocument.Parse(expectedJson);
    using var actual = JsonDocument.Parse(actualJson);

    (JsonElement.DeepEquals(expected.RootElement, actual.RootElement)).Should().BeTrue($"Expected JSON: {expected.RootElement}\nActual JSON: {actual.RootElement}");
  }
}
