using Hevy.Client.Models;
using Hevy.Client.Serialization;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using Xunit;

namespace Hevy.Client.Tests.Serialization;

public sealed class OpenApiSchemaContractTests
{
  [Fact]
  public void Handwritten_contracts_match_every_pinned_openapi_component()
  {
    OpenApiContract.AssertAllComponentsMatch(new Dictionary<string, Type>(StringComparer.Ordinal)
    {
      ["PostWorkoutsRequestSet"] = typeof(WorkoutSetWrite),
      ["PostWorkoutsRequestExercise"] = typeof(WorkoutExerciseWrite),
      ["PostWorkoutsRequestBody"] = typeof(CreateWorkoutRequest),
      ["PostRoutinesRequestSet"] = typeof(CreateRoutineSetWrite),
      ["PostRoutinesRequestExercise"] = typeof(CreateRoutineExerciseWrite),
      ["PostRoutinesRequestBody"] = typeof(CreateRoutineRequest),
      ["PutRoutinesRequestSet"] = typeof(UpdateRoutineSetWrite),
      ["PutRoutinesRequestExercise"] = typeof(UpdateRoutineExerciseWrite),
      ["PutRoutinesRequestBody"] = typeof(UpdateRoutineRequest),
      ["PostRoutineFolderRequestBody"] = typeof(CreateRoutineFolderRequest),
      ["BodyMeasurement"] = typeof(BodyMeasurement),
      ["PutBodyMeasurement"] = typeof(UpdateBodyMeasurementRequest),
      ["Set"] = typeof(WorkoutSet),
      ["Exercise"] = typeof(WorkoutExercise),
      ["ExerciseHistoryEntry"] = typeof(ExerciseHistoryEntry),
      ["CustomExerciseType"] = typeof(CustomExerciseType),
      ["MuscleGroup"] = typeof(MuscleGroup),
      ["EquipmentCategory"] = typeof(EquipmentCategory),
      ["ExerciseTemplate"] = typeof(ExerciseTemplate),
      ["CreateCustomExerciseRequestBody"] = typeof(CreateExerciseTemplateRequest),
      ["RoutineFolder"] = typeof(RoutineFolder),
      ["Routine"] = typeof(Routine),
      ["UserInfo"] = typeof(UserInfo),
      ["UserInfoResponse"] = typeof(UserInfoResponse),
      ["Workout"] = typeof(Workout),
      ["UpdatedWorkout"] = typeof(UpdatedWorkoutEvent),
      ["DeletedWorkout"] = typeof(DeletedWorkoutEvent),
      ["PaginatedWorkoutEvents"] = typeof(WorkoutEventsPage),
    });
  }

  [Fact]
  public void Handwritten_contracts_match_every_successful_endpoint_schema()
  {
    OpenApiContract.AssertEndpointsMatch(
        new Dictionary<(string Method, string Path), Type>
        {
          [("post", "/v1/workouts")] = typeof(CreateWorkoutRequest),
          [("put", "/v1/workouts/{workoutId}")] = typeof(UpdateWorkoutRequest),
          [("post", "/v1/routines")] = typeof(CreateRoutineRequest),
          [("put", "/v1/routines/{routineId}")] = typeof(UpdateRoutineRequest),
          [("post", "/v1/exercise_templates")] = typeof(CreateExerciseTemplateRequest),
          [("post", "/v1/routine_folders")] = typeof(CreateRoutineFolderRequest),
          [("post", "/v1/body_measurements")] = typeof(CreateBodyMeasurementRequest),
          [("put", "/v1/body_measurements/{date}")] = typeof(UpdateBodyMeasurementRequest),
        },
        new Dictionary<(string Method, string Path, string Status), Type>
        {
          [("get", "/v1/workouts", "200")] = typeof(WorkoutPage),
          [("post", "/v1/workouts", "201")] = typeof(Workout),
          [("get", "/v1/workouts/count", "200")] = typeof(WorkoutCountResponse),
          [("get", "/v1/workouts/events", "200")] = typeof(WorkoutEventsPage),
          [("get", "/v1/workouts/{workoutId}", "200")] = typeof(Workout),
          [("put", "/v1/workouts/{workoutId}", "200")] = typeof(Workout),
          [("get", "/v1/user/info", "200")] = typeof(UserInfoResponse),
          [("get", "/v1/routines", "200")] = typeof(RoutinePage),
          [("post", "/v1/routines", "201")] = typeof(Routine),
          [("get", "/v1/routines/{routineId}", "200")] = typeof(RoutineResponse),
          [("put", "/v1/routines/{routineId}", "200")] = typeof(Routine),
          [("get", "/v1/exercise_templates", "200")] = typeof(ExerciseTemplatePage),
          [("post", "/v1/exercise_templates", "200")] = typeof(CreateExerciseTemplateResponse),
          [("get", "/v1/exercise_templates/{exerciseTemplateId}", "200")] = typeof(ExerciseTemplate),
          [("get", "/v1/routine_folders", "200")] = typeof(RoutineFolderPage),
          [("post", "/v1/routine_folders", "201")] = typeof(RoutineFolder),
          [("get", "/v1/routine_folders/{folderId}", "200")] = typeof(RoutineFolder),
          [("get", "/v1/exercise_history/{exerciseTemplateId}", "200")] = typeof(ExerciseHistoryResponse),
          [("get", "/v1/body_measurements", "200")] = typeof(BodyMeasurementPage),
          [("get", "/v1/body_measurements/{date}", "200")] = typeof(BodyMeasurement),
        });
  }
}

internal static class OpenApiContract
{
  private static readonly NullabilityInfoContext Nullability = new();

  internal static void AssertAllComponentsMatch(IReadOnlyDictionary<string, Type> mappings)
  {
    var snapshot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/api/hevy-openapi-2026-07-26.json"));
    using var document = JsonDocument.Parse(File.ReadAllText(snapshot));
    var components = document.RootElement.GetProperty("components").GetProperty("schemas");
    (mappings.Keys.Order()).Should().Equal(components.EnumerateObject().Select(static schema => schema.Name).Order());
    foreach (var mapping in mappings)
    {
      AssertSchema(components.GetProperty(mapping.Key), mapping.Value, components, mapping.Key);
    }
  }

  internal static void AssertEndpointsMatch(
      IReadOnlyDictionary<(string Method, string Path), Type> requestMappings,
      IReadOnlyDictionary<(string Method, string Path, string Status), Type> responseMappings)
  {
    var snapshot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/api/hevy-openapi-2026-07-26.json"));
    using var document = JsonDocument.Parse(File.ReadAllText(snapshot));
    var root = document.RootElement;
    var components = root.GetProperty("components").GetProperty("schemas");
    foreach (var mapping in requestMappings)
    {
      var operation = root.GetProperty("paths").GetProperty(mapping.Key.Path).GetProperty(mapping.Key.Method);
      var schema = operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
      AssertSchema(schema, mapping.Value, components, $"{mapping.Key.Method.ToUpperInvariant()} {mapping.Key.Path} request");
    }
    foreach (var mapping in responseMappings)
    {
      var schema = root.GetProperty("paths").GetProperty(mapping.Key.Path).GetProperty(mapping.Key.Method)
          .GetProperty("responses").GetProperty(mapping.Key.Status).GetProperty("content").GetProperty("application/json").GetProperty("schema");
      AssertSchema(schema, mapping.Value, components, $"{mapping.Key.Method.ToUpperInvariant()} {mapping.Key.Path} {mapping.Key.Status}");
    }
  }

  private static void AssertSchema(JsonElement schema, Type declaredType, JsonElement components, string path)
  {
    var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
    if (schema.TryGetProperty("$ref", out var reference))
    {
      var name = reference.GetString()!.Split('/')[^1];
      AssertSchema(components.GetProperty(name), type, components, $"{path}->$ref:{name}");
      return;
    }

    if (schema.TryGetProperty("enum", out var enumValues))
    {
      if (type == typeof(WorkoutRpe))
      {
        (enumValues.EnumerateArray().Select(static value => value.GetDecimal())).Should().Equal(new[] { 6m, 7m, 7.5m, 8m, 8.5m, 9m, 9.5m, 10m });
        return;
      }
      (type.IsEnum).Should().BeTrue($"{path} is an enum in OpenAPI but maps to {type.Name}.");
      var actual = Enum.GetNames(type)
          .Select(name => type.GetField(name)!.GetCustomAttributes(typeof(JsonStringEnumMemberNameAttribute), false)
              .Cast<JsonStringEnumMemberNameAttribute>().SingleOrDefault()?.Name ?? name.ToLowerInvariant())
          .Order(StringComparer.Ordinal)
          .ToArray();
      var expected = enumValues.EnumerateArray().Select(static value => value.GetString()).Order(StringComparer.Ordinal).ToArray();
      (actual).Should().Equal(expected);
      return;
    }

    var schemaType = schema.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : "object";
    if (schemaType == "array")
    {
      AssertSchema(schema.GetProperty("items"), CollectionElementType(type), components, $"{path}[]");
      return;
    }
    if (schemaType != "object")
    {
      AssertPrimitive(schema, schemaType!, type, path);
      return;
    }

    var typeInfo = HevyJsonContext.Default.GetTypeInfo(type) ?? throw new InvalidOperationException($"{type.Name} is absent from HevyJsonContext.");
    (typeInfo.Kind).Should().Be(JsonTypeInfoKind.Object);
    var actualProperties = typeInfo.Properties.Select(static property => property.Name).ToHashSet(StringComparer.Ordinal);
    if (type == typeof(UpdatedWorkoutEvent) || type == typeof(DeletedWorkoutEvent)) actualProperties.Add("type");
    var properties = schema.TryGetProperty("properties", out var propertyElement) ? propertyElement : default;
    var expectedProperties = properties.ValueKind == JsonValueKind.Object
        ? properties.EnumerateObject().Select(static property => property.Name).ToHashSet(StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);
    (expectedProperties.SetEquals(actualProperties)).Should().BeTrue($"{path} properties differ. OpenAPI-only: {string.Join(", ", expectedProperties.Except(actualProperties))}; DTO-only: {string.Join(", ", actualProperties.Except(expectedProperties))}.");

    var required = schema.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array
        ? requiredElement.EnumerateArray().Select(static property => property.GetString()!).ToHashSet(StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);
    var enforced = typeInfo.Properties
        .Where(static property => property.IsRequired || property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null)
        .Select(static property => property.Name)
        .ToHashSet(StringComparer.Ordinal);
    if (type == typeof(UpdatedWorkoutEvent) || type == typeof(DeletedWorkoutEvent)) enforced.Add("type");
    (enforced.IsSupersetOf(required)).Should().BeTrue($"{path} does not enforce required OpenAPI fields: {string.Join(", ", required.Except(enforced))}.");

    foreach (var property in typeInfo.Properties)
    {
      var propertySchema = properties.GetProperty(property.Name);
      AssertNullable(propertySchema, property, $"{path}.{property.Name}");
      AssertSchema(propertySchema, property.PropertyType, components, $"{path}.{property.Name}");
    }
  }

  private static Type CollectionElementType(Type type)
  {
    if (type.IsArray) return type.GetElementType()!;
    var enumerable = type.GetInterfaces().Append(type)
        .FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    (enumerable).Should().NotBeNull($"{type.Name} must map to an OpenAPI collection");
    return enumerable!.GetGenericArguments()[0];
  }

  private static void AssertPrimitive(JsonElement schema, string schemaType, Type type, string path)
  {
    var valid = schemaType switch
    {
      "string" => type == typeof(string) || type == typeof(DateOnly) || type == typeof(DateTimeOffset),
      "boolean" => type == typeof(bool),
      "integer" => type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long),
      "number" => type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) || type == typeof(decimal),
      _ => false,
    };
    (valid).Should().BeTrue($"{path} is OpenAPI {schemaType} but maps to {type.Name}.");

    if (schema.TryGetProperty("format", out var formatElement))
    {
      var format = formatElement.GetString();
      var formatMatches = format switch
      {
        "date" => type == typeof(DateOnly),
        "date-time" => type == typeof(DateTimeOffset),
        "uuid" => type == typeof(string),
        _ => false,
      };
      (formatMatches).Should().BeTrue($"{path} uses OpenAPI format {format} but maps to {type.Name}.");
    }
  }

  private static void AssertNullable(JsonElement schema, JsonPropertyInfo property, string path)
  {
    var schemaNullable = schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean();
    if (!schemaNullable) return;
    var typeNullable = Nullable.GetUnderlyingType(property.PropertyType) is not null;
    if (!property.PropertyType.IsValueType && property.AttributeProvider is PropertyInfo member)
    {
      typeNullable = Nullability.Create(member).ReadState != NullabilityState.NotNull;
    }
    (typeNullable).Should().BeTrue($"{path} is nullable in OpenAPI but not in the handwritten DTO.");
  }
}
