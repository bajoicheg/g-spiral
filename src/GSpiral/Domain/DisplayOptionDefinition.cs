namespace GSpiral.Domain;

public sealed record DisplayOptionDefinition(
    string Id,
    int StageIndex,
    string Text,
    IReadOnlyList<string> AtomicOptionIds);
