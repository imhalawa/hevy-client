using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

public sealed record CreateExerciseTemplateRequest(CustomExerciseWriteRequest Exercise)
{
	public static implicit operator CreateExerciseTemplateRequest(CreateExerciseTemplateCommand value)
	{
		return new CreateExerciseTemplateRequest(value.Exercise.ToRequest());
	}

	public static implicit operator CreateExerciseTemplateCommand(CreateExerciseTemplateRequest value)
	{
		return value.ToCommand();
	}
}
