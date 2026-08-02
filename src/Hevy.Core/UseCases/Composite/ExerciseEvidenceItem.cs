namespace Hevy.Core.UseCases;

public sealed record ExerciseEvidenceItem(
    string ExerciseTemplateId,
    string Title,
    decimal VolumeKgReps,
    int CountedSets);
