using System.Globalization;
using GSpiral.Domain;

namespace GSpiral.Presentation;

public sealed record ResultRowViewModel(
    CultureTypeId TypeId,
    string Name,
    string PrimaryHex,
    int Score,
    double Share,
    double BarFraction)
{
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    public string ScoreAndShareText =>
        $"{Score} из 7 · {(Share * 100d).ToString("0.0", RussianCulture)}%";
}
