namespace GSpiral.Domain;

public sealed record CellDefinition(
    string Id,
    CultureTypeId TypeId,
    int StageIndex,
    IReadOnlyList<AtomicOptionDefinition> Options);
