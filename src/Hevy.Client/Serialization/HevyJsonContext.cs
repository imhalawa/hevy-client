using System.Text.Json.Serialization;
using Hevy.Client.Models;

namespace Hevy.Client.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(WorkoutResponse))]
[JsonSerializable(typeof(WorkoutPageResponse))]
[JsonSerializable(typeof(WorkoutCountResponse))]
[JsonSerializable(typeof(WorkoutEventsPageResponse))]
[JsonSerializable(typeof(RoutineResponse))]
[JsonSerializable(typeof(RoutinePageResponse))]
[JsonSerializable(typeof(RoutineEnvelopeResponse))]
[JsonSerializable(typeof(RoutineFolderResponse))]
[JsonSerializable(typeof(RoutineFolderPageResponse))]
[JsonSerializable(typeof(ExerciseTemplateResponse))]
[JsonSerializable(typeof(ExerciseTemplatePageResponse))]
[JsonSerializable(typeof(CreateExerciseTemplateResponse))]
[JsonSerializable(typeof(ExerciseHistoryEntryResponse))]
[JsonSerializable(typeof(ExerciseHistoryResponse))]
[JsonSerializable(typeof(BodyMeasurementResponse))]
[JsonSerializable(typeof(BodyMeasurementPageResponse))]
[JsonSerializable(typeof(UserInfoResponse))]
[JsonSerializable(typeof(UpdatedWorkoutEventResponse))]
[JsonSerializable(typeof(DeletedWorkoutEventResponse))]
[JsonSerializable(typeof(CreateWorkoutRequest))]
[JsonSerializable(typeof(UpdateWorkoutRequest))]
[JsonSerializable(typeof(WorkoutWriteRequest))]
[JsonSerializable(typeof(WorkoutExerciseWriteRequest))]
[JsonSerializable(typeof(WorkoutSetWriteRequest))]
[JsonSerializable(typeof(CreateRoutineRequest))]
[JsonSerializable(typeof(UpdateRoutineRequest))]
[JsonSerializable(typeof(CreateRoutineFolderRequest))]
[JsonSerializable(typeof(CreateExerciseTemplateRequest))]
[JsonSerializable(typeof(CreateBodyMeasurementRequest))]
[JsonSerializable(typeof(UpdateBodyMeasurementRequest))]
public sealed partial class HevyJsonContext : JsonSerializerContext;
