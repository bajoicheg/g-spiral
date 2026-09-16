using GSpiral.Domain;

namespace GSpiral.Presentation;

public sealed record ResultRowViewModel(
    CultureTypeId TypeId,
    string Name,
    string PrimaryHex,
    int Score,
    double Share,
    double BarFraction);
