namespace Hevy.Mcp.Composite;

internal sealed record ExerciseEvidenceItem(
    string ExerciseTemplateId,
    string Title,
    decimal VolumeKgReps,
    int CountedSets);
