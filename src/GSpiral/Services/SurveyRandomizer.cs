using GSpiral.Domain;

namespace GSpiral.Services;

public static class SurveyRandomizer
{
    public static IReadOnlyList<IReadOnlyList<string>> CreateOrders(int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var stages = new List<IReadOnlyList<string>>(SurveyCatalog.Stages.Count);

        foreach (var stage in SurveyCatalog.Stages)
        {
            var ids = SurveyCatalog.OptionsForStage(stage.Index)
                .Select(option => option.Id)
                .ToArray();
            Shuffle(ids, random);
            stages.Add(ids);
        }

        return stages;
    }

    public static IReadOnlyList<IReadOnlyList<string>> CreateDisplayOrders(int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var stages = new List<IReadOnlyList<string>>(SurveyCatalog.Stages.Count);

        foreach (var stage in SurveyCatalog.Stages)
        {
            var ids = SurveyDisplayCatalog.OptionsForStage(stage.Index)
                .Select(option => option.Id)
                .ToArray();
            Shuffle(ids, random);
            stages.Add(ids);
        }

        return stages;
    }

    public static SurveyRunState CreateRun(int? seed = null) =>
        new(CreateDisplayOrders(seed));

    private static void Shuffle(string[] values, Random random)
    {
        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
