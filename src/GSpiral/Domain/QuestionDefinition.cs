namespace GSpiral.Domain;

public sealed record QuestionDefinition(
    int Index,
    string Title,
    IReadOnlyDictionary<CultureTypeId, string> Options);
