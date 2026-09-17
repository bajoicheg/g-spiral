namespace GSpiral.Domain;

public sealed class SurveyRunState
{
    private readonly HashSet<string> selections = new(StringComparer.Ordinal);

    public SurveyRunState(IReadOnlyList<IReadOnlyList<string>>? randomizedOrderByStage = null)
    {
        RandomizedOrderByStage = randomizedOrderByStage ?? [];
    }

    public IReadOnlyList<IReadOnlyList<string>> RandomizedOrderByStage { get; private set; }

    public IReadOnlyCollection<string> SelectedOptionIds => selections;

    public bool IsSelected(string optionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);
        return selections.Contains(optionId);
    }

    public void SetSelected(string optionId, bool selected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);
        if (selected)
        {
            selections.Add(optionId);
        }
        else
        {
            selections.Remove(optionId);
        }
    }

    public void SetRandomizedOrder(IReadOnlyList<IReadOnlyList<string>> orderByStage)
    {
        ArgumentNullException.ThrowIfNull(orderByStage);
        RandomizedOrderByStage = orderByStage;
    }

    public void ResetSelections() => selections.Clear();
}
