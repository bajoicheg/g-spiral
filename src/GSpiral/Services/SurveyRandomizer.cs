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

            for (var i = ids.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }

            stages.Add(ids);
        }

        return stages;
    }
}
