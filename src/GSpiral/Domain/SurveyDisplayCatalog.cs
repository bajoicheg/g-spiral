using System.Text;

namespace GSpiral.Domain;

public static class SurveyDisplayCatalog
{
    private static readonly IReadOnlyList<DisplayOptionDefinition>[] OptionsByStage =
        SurveyCatalog.Stages
            .Select(stage => BuildStage(stage.Index))
            .ToArray();

    public static IReadOnlyList<DisplayOptionDefinition> OptionsForStage(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= OptionsByStage.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(stageIndex));
        }

        return OptionsByStage[stageIndex];
    }

    public static DisplayOptionDefinition GetOption(int stageIndex, string displayOptionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayOptionId);
        return OptionsForStage(stageIndex)
            .Single(option => string.Equals(option.Id, displayOptionId, StringComparison.Ordinal));
    }

    private static IReadOnlyList<DisplayOptionDefinition> BuildStage(int stageIndex)
    {
        var groups = new List<(string Key, string Text, List<string> AtomicIds)>();
        var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var option in SurveyCatalog.OptionsForStage(stageIndex))
        {
            var key = NormalizeKey(option.Text);
            if (!indexByKey.TryGetValue(key, out var groupIndex))
            {
                groupIndex = groups.Count;
                indexByKey.Add(key, groupIndex);
                groups.Add((key, option.Text.Trim(), []));
            }

            groups[groupIndex].AtomicIds.Add(option.Id);
        }

        return groups
            .Select((group, index) => new DisplayOptionDefinition(
                $"s{stageIndex}-d{index}",
                stageIndex,
                group.Text,
                group.AtomicIds.ToArray()))
            .ToArray();
    }

    private static string NormalizeKey(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var normalized = text.Normalize(NormalizationForm.FormKC);
        return string.Join(
                ' ',
                normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .ToUpperInvariant();
    }
}
