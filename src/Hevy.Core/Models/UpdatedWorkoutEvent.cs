namespace Hevy.Core.Models;

public sealed record UpdatedWorkoutEvent(Workout Workout) : WorkoutEvent;
