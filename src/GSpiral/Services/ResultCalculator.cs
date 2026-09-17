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

    public static double CellScore(SurveyRunState state, CellDefinition cell)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(cell);
        if (cell.Options.Count == 0)
        {
            return 0d;
        }

        var selected = cell.Options.Count(option => state.IsSelected(option.Id));
        return 100d * selected / cell.Options.Count;
    }

    public static IReadOnlyList<SurveyScoreResult> Calculate(SurveyRunState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var scored = SurveyCatalog.Types
            .Select(type => new
            {
                Type = type,
                Score = SurveyCatalog.Cells
                    .Where(cell => cell.TypeId == type.Id)
                    .Sum(cell => CellScore(state, cell))
            })
            .ToArray();

        var total = scored.Sum(x => x.Score);

        return scored
            .Select(x => new SurveyScoreResult(
                x.Type.Id,
                x.Type.Name,
                x.Type.PrimaryHex,
                x.Score,
                x.Score / 42d,
                total == 0d ? 0d : x.Score / total,
                x.Type.Order))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.CanonicalOrder)
            .ToArray();
    }
}
