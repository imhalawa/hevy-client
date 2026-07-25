# Task 2: Typed models and serialization contract

## Implementation

Implemented immutable typed records for every response and request family in the checked-in 14-path Hevy OpenAPI snapshot. The public model surface is grouped by capability under `src/Hevy.Client/Models/`, with shared pagination and metric records in `Common.cs`.

`HevyJsonContext` source-generates metadata for every endpoint response envelope and mutation request root. It applies snake_case names and explicit enum-member wire literals. Response parsing remains forward-compatible because the default `System.Text.Json` unmapped-member behavior ignores additive properties.

Sanitized, official-shaped JSON fixtures and test-only fixture helpers live under `tests/TestSupport/`. Serialization tests cover response fields, unknown workout fields, the workout-event union, all endpoint envelopes, and every create/update request family.

## RED/GREEN evidence

1. Response models RED
   - Command: `dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~Serialization`
   - Observed: compile errors `CS0234` for absent `Hevy.Client.Models` and `Hevy.Client.Serialization` namespaces.
   - Expected failure: tests referenced the not-yet-created response records and source-generated context.
   - GREEN: same focused command passed 8 response-deserialization tests with zero warnings.

2. Request models RED
   - Command: `dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~Serialization`
   - Observed: `CS0246` for absent `CreateWorkoutRequest`, `UpdateWorkoutRequest`, routine, folder, template, and measurement request records.
   - Expected failure: serialization tests referenced the missing writable-only request DTOs and context metadata.
   - GREEN: focused suite passed 16 tests with zero warnings after the request records and metadata were added.

3. Enum wire-literal defect
   - Command: `dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~Create_exercise_template_serializes_enum_wire_values`
   - Observed: expected `"exercise_type":"weight_reps"` was absent; a minimal diagnostic assertion confirmed the generated converter emitted `WeightReps`.
   - Expected failure: default enum conversion used CLR names rather than official snake_case literals.
   - GREEN: restoring the literal assertion and adding `JsonStringEnumMemberName` attributes passed the focused suite (16 tests) with zero warnings.

4. Endpoint-envelope models RED
   - Command: `dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~Serialization`
   - Observed: `CS1061` for absent `HevyJsonContext` metadata such as `WorkoutPage`, `RoutineResponse`, and `BodyMeasurementPage`.
   - Expected failure: each endpoint envelope test intentionally referenced metadata removed before its test-first implementation.
   - GREEN: focused suite passed 25 tests with zero warnings.

Final verification before commit:

```text
dotnet build --no-restore -c Release
6 projects, 0 errors, 0 warnings

dotnet test --no-restore -c Release
25 tests passed, 0 warnings in 3 projects
```

## Files changed

- `src/Hevy.Client/Models/Common.cs`, `Workouts.cs`, `Routines.cs`, `Exercises.cs`, `Measurements.cs`, and `User.cs`
- `src/Hevy.Client/Serialization/HevyJsonContext.cs`
- `tests/Hevy.Client.Tests/Serialization/*Tests.cs` and project fixture links
- `tests/TestSupport/Fixture.cs`, `FixtureFactory.cs`, and sanitized `Fixtures/*.json`
- `docs/superpowers/progress/2026-07-26-hevy-client-progress.md`

## Self-review

- Compared every model family and endpoint envelope against `docs/api/hevy-openapi-2026-07-26.json`; its version/path check returned `true` for version `0.0.1` and 14 paths.
- Corrected create-routine rep-range bounds to non-null decimals; update and response variants preserve the nullable bounds allowed by their contracts.
- Verified snake_case enum serialization with literal request assertions, including nested enum arrays.
- Confirmed request records exclude response-only identifiers and timestamps; response tests demonstrate unknown additive-field tolerance.

## Concerns

- The official schema declares response `RoutineExercise.rest_seconds` as a string while the corresponding request field is an integer; the model intentionally preserves that wire distinction.
- Shared response set metrics use decimal values so workouts/routines (`number`) and history (`integer`) retain a single reusable metric shape. No unresolved blocker remains.

## Fix Round 1

Addressed all three Important review findings.

1. Strict outgoing set values and RPE
   - RED command: `dotnet test tests/Hevy.Client.Tests --filter "FullyQualifiedName~Workout_set_write_rejects_unknown_set_type|FullyQualifiedName~Workout_rpe_rejects_undocumented_value"`
   - Observed: compile errors `CS0246` because `SetType` and `WorkoutRpe` did not exist.
   - Expected failure: mutation DTOs exposed unrestricted `string`/`decimal?` values, so the required closed wire-value API was absent.
   - GREEN command: same focused command.
   - Output: `2 tests passed, 0 warnings in 1 projects`.
   - Coverage: `RequestSerializationTests.cs` proves an invalid cast `SetType` fails during serialization and `WorkoutRpe(8.25m)` rejects construction. The complete create-workout JSON-tree assertion proves a valid RPE serializes as the numeric `8.5` literal.

2. Forward-compatible exercise-template muscles
   - RED command: `dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~Exercise_template_accepts_additive_muscle_names`
   - Observed: `JsonException` at `$.primary_muscle_group` while parsing `serratus_anterior`.
   - Expected failure: response models incorrectly used the closed `MuscleGroup` mutation enum.
   - GREEN command: same focused command.
   - Output: `1 tests passed, 0 warnings in 1 projects`.
   - Coverage: `ResponseDeserializationTests.cs` injects unknown primary and secondary muscle names and asserts both raw strings survive parsing.

3. Complete request-payload contract assertions
   - Replaced selected substring assertions in `RequestSerializationTests.cs` with `JsonDocument`/`JsonElement.DeepEquals` comparisons against independently written JSON literals.
   - Coverage: all eight create/update request roots now compare their complete nested property trees, making additions, omissions, wrong wire names, and server-owned fields observable.
   - Focused verification: `dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~RequestSerializationTests` returned `10 tests passed, 0 warnings in 1 projects`.

Final fix-round verification:

```text
dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~Serialization
all serialization tests passed with zero warnings

dotnet build --no-restore -c Release
6 projects, 0 errors, 0 warnings

dotnet test --no-restore -c Release
all tests passed with zero warnings
```

The prior minor review item about widening integer exercise-history values is intentionally unchanged and remains for final review.
