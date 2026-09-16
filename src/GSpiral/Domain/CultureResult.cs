namespace GSpiral.Domain;

public sealed record CultureResult(
    CultureTypeId TypeId,
    string Name,
    string PrimaryHex,
    int Score,
    double Share,
    int CanonicalOrder);
