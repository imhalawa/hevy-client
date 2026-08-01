using System;

namespace Hevy.Core.Models;

public sealed record DeletedWorkoutEvent(string Id, DateTimeOffset DeletedAt) : WorkoutEvent;
