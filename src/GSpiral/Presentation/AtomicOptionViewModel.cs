using System.Windows.Input;

namespace GSpiral.Presentation;

public sealed class AtomicOptionViewModel : ObservableObject
{
    private readonly Action<bool> selectionChanged;
    private bool isSelected;

    public AtomicOptionViewModel(
        string optionId,
        string text,
        string decorativeFill,
        string decorativeBorder,
        bool isSelected,
        Action<bool> selectionChanged)
    {
        OptionId = optionId;
        Text = text;
        DecorativeFill = decorativeFill;
        DecorativeBorder = decorativeBorder;
        this.isSelected = isSelected;
        this.selectionChanged = selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));
        ToggleCommand = new RelayCommand(_ => IsSelected = !IsSelected);
    }

    public string OptionId { get; }
    public string Text { get; }
    public string DecorativeFill { get; }
    public string DecorativeBorder { get; }
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
