using GSpiral.Domain;

namespace GSpiral.Services;

public static class ResultCalculator
{
    public static IReadOnlyList<CultureResult> Calculate(SurveyState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var scored = SurveyCatalog.Types
            .Select(type => new
            {
                Type = type,
                Score = Enumerable.Range(0, SurveyCatalog.Questions.Count)
                    .Count(q => state.IsSelected(q, type.Id))
            })
            .ToArray();

        var total = scored.Sum(x => x.Score);

        return scored
            .Select(x => new CultureResult(
                x.Type.Id,
                x.Type.Name,
                x.Type.PrimaryHex,
                x.Score,
                total == 0 ? 0d : (double)x.Score / total,
                x.Type.Order))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.CanonicalOrder)
            .ToArray();
    }
}
