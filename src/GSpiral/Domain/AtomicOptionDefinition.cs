namespace GSpiral.Domain;

public sealed record AtomicOptionDefinition(
    string Id,
    string CellId,
    int StageIndex,
    string Text);
