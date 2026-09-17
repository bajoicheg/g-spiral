namespace GSpiral.Presentation;

public sealed record StageAnswerSummaryViewModel(
    int StageIndex,
    string Title,
    IReadOnlyList<string> SelectedTexts)
{
    public string CountText => $"Выбрано: {SelectedTexts.Count}";
}
