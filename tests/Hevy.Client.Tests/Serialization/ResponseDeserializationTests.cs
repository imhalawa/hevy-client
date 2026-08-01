using System.Text.Json;
using Hevy.Core.Models;
using Hevy.Client.Contracts;
using Hevy.Client.Serialization;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests.Serialization;

public sealed class ResponseDeserializationTests
{
  [Fact]
  public void Workout_accepts_unknown_additive_fields()
  {
    var json = Fixture.Read("workout.json").Replace("\"title\":", "\"future_field\":42,\"title\":", StringComparison.Ordinal);

    var workout = JsonSerializer.Deserialize(json, HevyJsonContext.Default.WorkoutResponse);

    (workout!.Id).Should().Be("workout-1");
    (workout.Exercises[0].Title).Should().Be("Bench Press (Barbell)");
    (workout.Exercises[0].Sets[0].WeightKg).Should().Be(100);
  }

  [Fact]
  public void Routine_deserializes_nested_set_metrics()
  {
    var routine = JsonSerializer.Deserialize(Fixture.Read("routine.json"), HevyJsonContext.Default.RoutineResponse);

    (routine!.Id).Should().Be("routine-1");
    (routine.FolderId).Should().Be(42);
    (routine.Exercises[0].Sets[0].RepRange!.Start).Should().Be(8);
  }

  [Fact]
  public void Exercise_template_accepts_additive_muscle_names()
  {
    var json = Fixture.Read("exercise-template.json")
        .Replace("\"chest\"", "\"serratus_anterior\"", StringComparison.Ordinal)
        .Replace("\"triceps\"", "\"teres_major\"", StringComparison.Ordinal);
    var template = JsonSerializer.Deserialize(json, HevyJsonContext.Default.ExerciseTemplateResponse);

    (template!.Id).Should().Be("D04AC939");
    (template.PrimaryMuscleGroup).Should().Be("serratus_anterior");
    (template.SecondaryMuscleGroups[0]).Should().Be("teres_major");
  }

  [Fact]
  public void Exercise_history_deserializes_set_metrics()
  {
    var entry = JsonSerializer.Deserialize(Fixture.Read("exercise-history.json"), HevyJsonContext.Default.ExerciseHistoryEntryResponse);

    (entry!.WorkoutId).Should().Be("workout-1");
    (entry.Reps).Should().Be(10);
    (entry.SetType).Should().Be("normal");
  }

  [Fact]
  public void Body_measurement_deserializes_date_and_metrics()
  {
    var measurement = JsonSerializer.Deserialize(Fixture.Read("body-measurement.json"), HevyJsonContext.Default.BodyMeasurementResponse);

    (measurement!.Date).Should().Be(new DateOnly(2024, 8, 14));
    (measurement.WeightKg!.Value).Should().Be(80.5m);
    (measurement.RightCalf!.Value).Should().Be(37.5m);
  }

  [Fact]
  public void User_info_deserializes_data_envelope()
  {
    var response = JsonSerializer.Deserialize(Fixture.Read("user-info.json"), HevyJsonContext.Default.UserInfoResponse);

    (response!.Data.Id).Should().Be("user-1");
    (response.Data.Name).Should().Be("Sanitized User");
  }

  [Fact]
  public void Routine_folder_deserializes_identity_and_order()
  {
    var folder = JsonSerializer.Deserialize(Fixture.Read("routine-folder.json"), HevyJsonContext.Default.RoutineFolderResponse);

    (folder!.Id).Should().Be(42);
    (folder.Index).Should().Be(1);
    (folder.Title).Should().Be("Push Pull");
  }

  [Fact]
  public void Workout_events_deserialize_updated_and_deleted_variants()
  {
    var events = JsonSerializer.Deserialize(Fixture.Read("workout-events.json"), HevyJsonContext.Default.WorkoutEventsPageResponse);

    var updated = (events!.Events[0]).Should().BeOfType<UpdatedWorkoutEventResponse>().Which;
    var deleted = (events.Events[1]).Should().BeOfType<DeletedWorkoutEventResponse>().Which;
    (updated.Workout.Id).Should().Be("workout-1");
    (deleted.Id).Should().Be("workout-2");
  }
}
