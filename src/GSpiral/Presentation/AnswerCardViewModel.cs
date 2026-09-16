using System.Windows.Input;
using GSpiral.Domain;

namespace GSpiral.Presentation;

public sealed class AnswerCardViewModel : ObservableObject
{
    private readonly Action<bool> selectionChanged;
    private bool isSelected;

    public AnswerCardViewModel(
        CultureTypeId typeId,
        string typeName,
        string text,
        string primaryHex,
        string lightHex,
        bool isSelected,
        Action<bool> selectionChanged)
    {
        TypeId = typeId;
        TypeName = typeName;
        Text = text;
        PrimaryHex = primaryHex;
        LightHex = lightHex;
        this.isSelected = isSelected;
        this.selectionChanged = selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));
        ToggleCommand = new RelayCommand(_ => IsSelected = !IsSelected);
    }

    public CultureTypeId TypeId { get; }
    public string TypeName { get; }
    public string Text { get; }
    public string PrimaryHex { get; }
    public string LightHex { get; }
    public ICommand ToggleCommand { get; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (SetProperty(ref isSelected, value))
            {
                selectionChanged(value);
            }
        }
    }
}
