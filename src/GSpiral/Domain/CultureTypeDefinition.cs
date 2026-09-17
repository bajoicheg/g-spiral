namespace GSpiral.Domain;

public sealed record CultureTypeDefinition(
    CultureTypeId Id,
    string Name,
    string PrimaryHex,
    string LightHex,
    int Order);
