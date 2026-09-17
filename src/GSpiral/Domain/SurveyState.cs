namespace GSpiral.Domain;

public sealed class SurveyState
{
    private readonly bool[,] selections = new bool[7, 6];

    public bool IsSelected(int questionIndex, CultureTypeId typeId) =>
        selections[ValidateQuestion(questionIndex), ValidateType(typeId)];

    public void SetSelected(int questionIndex, CultureTypeId typeId, bool selected) =>
        selections[ValidateQuestion(questionIndex), ValidateType(typeId)] = selected;

    public void Reset() => Array.Clear(selections, 0, selections.Length);

    private static int ValidateQuestion(int index) =>
        index is >= 0 and < 7 ? index : throw new ArgumentOutOfRangeException(nameof(index));

    private static int ValidateType(CultureTypeId typeId)
    {
        var index = (int)typeId;
        return index is >= 0 and < 6 ? index : throw new ArgumentOutOfRangeException(nameof(typeId));
    }
}
