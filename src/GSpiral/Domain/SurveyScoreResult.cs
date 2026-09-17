namespace GSpiral.Domain;

public sealed record SurveyScoreResult(
    CultureTypeId TypeId,
    string Name,
    string PrimaryHex,
    double Score,
    double AbsolutePercent,
    double ChartShare,
    int CanonicalOrder);
