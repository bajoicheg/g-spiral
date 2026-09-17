using System.Globalization;
using GSpiral.Domain;

namespace GSpiral.Presentation;

public sealed record ResultRowViewModel(
    CultureTypeId TypeId,
    string Name,
    string PrimaryHex,
    double Score,
    double AbsolutePercent,
    double ChartShare,
    double BarFraction)
{
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    public string ScoreText => $"{FormatRoundedScore(Score)} / 700";
    public string AbsolutePercentText => $"{AbsolutePercent.ToString("0.0", RussianCulture)}% выраженности";
    public string ScoreAndPercentText => $"{ScoreText} · {AbsolutePercentText}";

    public static string FormatRoundedScore(double score) =>
        Math.Round(score, 0, MidpointRounding.AwayFromZero)
            .ToString("0", RussianCulture);
}
