using System.Windows.Input;
using GSpiral.Domain;
using GSpiral.Services;

namespace GSpiral.Presentation;

public enum AppScreen
{
    Start,
    Question,
    Results
}

public sealed class MainViewModel : ObservableObject
{
    private static readonly (string Fill, string Border)[] DecorativePalette =
    [
        ("#EEF2FF", "#818CF8"),
        ("#FDF2F8", "#F472B6"),
        ("#ECFDF3", "#34D399"),
        ("#FFF7ED", "#FB923C"),
        ("#F5F3FF", "#A78BFA"),
        ("#ECFEFF", "#22D3EE"),
        ("#FEFCE8", "#EAB308"),
        ("#F0FDFA", "#2DD4BF"),
    ];

    private readonly RelayCommand startCommand;
    private readonly UserIdentityDefaults identityDefaults;
    private readonly Func<int?> seedProvider;
    private string respondentName = string.Empty;
    private string companyName = string.Empty;
    private string trimmedRespondentName = string.Empty;
    private string trimmedCompanyName = string.Empty;
    private AppScreen screen = AppScreen.Start;
    private int currentQuestionIndex;
    private IReadOnlyList<AtomicOptionViewModel> currentOptions = Array.Empty<AtomicOptionViewModel>();
    private IReadOnlyList<ResultRowViewModel> results = Array.Empty<ResultRowViewModel>();
    private IReadOnlyList<StageAnswerSummaryViewModel> stageAnswerSummaries = Array.Empty<StageAnswerSummaryViewModel>();
    private int totalSelections;
    private bool hasSelections;
    private bool isEditingIdentity;
    private string lastSavedReportPath = string.Empty;
    private SurveyRunState surveyRunState;

    public MainViewModel()
        : this(UserIdentityDefaults.Detect(), () => Random.Shared.Next())
    {
    }

    public MainViewModel(UserIdentityDefaults identityDefaults)
        : this(identityDefaults, () => Random.Shared.Next())
    {
    }

    public MainViewModel(UserIdentityDefaults identityDefaults, Func<int?> seedProvider)
    {
        this.identityDefaults = identityDefaults ?? throw new ArgumentNullException(nameof(identityDefaults));
        this.seedProvider = seedProvider ?? throw new ArgumentNullException(nameof(seedProvider));
        respondentName = identityDefaults.RespondentName;
        companyName = identityDefaults.CompanyName;
        trimmedRespondentName = respondentName.Trim();
        trimmedCompanyName = companyName.Trim();
        surveyRunState = CreateNewRunState();

        startCommand = new RelayCommand(_ => Start(), _ => CanStart());
        StartCommand = startCommand;
        BackCommand = new RelayCommand(_ => Back());
        NextCommand = new RelayCommand(_ => Next());
        RestartCommand = new RelayCommand(_ => Restart());
        BackToAnswersCommand = new RelayCommand(_ => BackToAnswers());
        EditIdentityCommand = new RelayCommand(_ => EditIdentity());
        SelectAllCommand = new RelayCommand(_ => SelectAllCurrent());
        ClearAllCommand = new RelayCommand(_ => ClearAllCurrent());
    }

    public SurveyRunState SurveyRunState
    {
        get => surveyRunState;
        private set => SetProperty(ref surveyRunState, value);
    }

    public string RespondentName
    {
        get => respondentName;
        set
        {
            if (SetProperty(ref respondentName, value))
            {
                startCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IdentitySummaryText));
                if (Screen == AppScreen.Results || IsEditingIdentity)
                {
                    ClearSavedReport();
                }
            }
        }
    }

    public string CompanyName
    {
        get => companyName;
        set
        {
            if (SetProperty(ref companyName, value))
            {
                startCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IdentitySummaryText));
                if (Screen == AppScreen.Results || IsEditingIdentity)
                {
                    ClearSavedReport();
                }
            }
        }
    }

    public string TrimmedRespondentName
    {
        get => trimmedRespondentName;
        private set => SetProperty(ref trimmedRespondentName, value);
    }

    public string TrimmedCompanyName
    {
        get => trimmedCompanyName;
        private set => SetProperty(ref trimmedCompanyName, value);
    }

    public AppScreen Screen
    {
        get => screen;
        private set => SetProperty(ref screen, value);
    }

    public int CurrentQuestionIndex
    {
        get => currentQuestionIndex;
        private set
        {
            if (SetProperty(ref currentQuestionIndex, value))
            {
                NotifyQuestionProperties();
            }
        }
    }

    public string CurrentQuestionTitle => SurveyCatalog.Stages[CurrentQuestionIndex].Title;
    public string QuestionProgressText => $"Вопрос {CurrentQuestionIndex + 1} из {SurveyCatalog.Stages.Count}";
    public string NextButtonText => CurrentQuestionIndex == SurveyCatalog.Stages.Count - 1 ? "Показать результаты" : "Далее →";
    public int CurrentQuestionSelectedCount => CurrentOptions.Count(option => option.IsSelected);
    public string CurrentQuestionSelectedCountText => $"Выбрано: {CurrentQuestionSelectedCount} из {CurrentOptions.Count}";

    public IReadOnlyList<AtomicOptionViewModel> CurrentOptions
    {
        get => currentOptions;
        private set
        {
            if (SetProperty(ref currentOptions, value))
            {
                NotifySelectionCount();
            }
        }
    }

    public IReadOnlyList<ResultRowViewModel> Results
    {
        get => results;
        private set => SetProperty(ref results, value);
    }

    public IReadOnlyList<StageAnswerSummaryViewModel> StageAnswerSummaries
    {
        get => stageAnswerSummaries;
        private set => SetProperty(ref stageAnswerSummaries, value);
    }

    public int TotalSelections
    {
        get => totalSelections;
        private set => SetProperty(ref totalSelections, value);
    }

    public bool HasSelections
    {
        get => hasSelections;
        private set => SetProperty(ref hasSelections, value);
    }

    public bool IsEditingIdentity
    {
        get => isEditingIdentity;
        private set
        {
            if (SetProperty(ref isEditingIdentity, value))
            {
                OnPropertyChanged(nameof(StartActionText));
                OnPropertyChanged(nameof(IdentityScreenTitle));
                OnPropertyChanged(nameof(IdentityScreenDescription));
            }
        }
    }

    public string LastSavedReportPath
    {
        get => lastSavedReportPath;
        private set
        {
            if (SetProperty(ref lastSavedReportPath, value))
            {
                OnPropertyChanged(nameof(HasSavedReport));
                OnPropertyChanged(nameof(SavedReportStatusText));
            }
        }
    }

    public bool HasSavedReport => !string.IsNullOrWhiteSpace(LastSavedReportPath);
    public string SavedReportStatusText => HasSavedReport ? $"Последний сохранённый файл: {LastSavedReportPath}" : string.Empty;
    public string StartActionText => IsEditingIdentity ? "Сохранить и вернуться к результатам" : "Начать";
    public string IdentityScreenTitle => IsEditingIdentity ? "Изменить данные респондента" : "Кто проходит опрос?";
    public string IdentityScreenDescription => IsEditingIdentity
        ? "Изменения применятся к результату и следующему XLSX. Ответы сохранятся."
        : "Имя и компания определены из Windows. При необходимости их можно исправить перед началом.";
    public string AppVersionText => $"{AppMetadata.ProductName} {AppMetadata.Version}";
    public DateOnly CurrentReportDate => DateOnly.FromDateTime(DateTime.Now);
    public string IdentitySummaryText => $"{RespondentName} · {CompanyName} · {CurrentReportDate:dd.MM.yyyy}";
    public string TotalSelectionsText => $"Всего выбрано утверждений: {TotalSelections}";
    public string ResultsPercentExplanation => "Процент выраженности показывает долю от максимально возможных 700 баллов для каждого типа.";
    public string PieShareExplanation => "Круговая диаграмма показывает долю типа в сумме набранных баллов.";

    public bool Progress1 => CurrentQuestionIndex >= 0;
    public bool Progress2 => CurrentQuestionIndex >= 1;
    public bool Progress3 => CurrentQuestionIndex >= 2;
    public bool Progress4 => CurrentQuestionIndex >= 3;
    public bool Progress5 => CurrentQuestionIndex >= 4;
    public bool Progress6 => CurrentQuestionIndex >= 5;
    public bool Progress7 => CurrentQuestionIndex >= 6;

    public ICommand StartCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand BackToAnswersCommand { get; }
    public ICommand EditIdentityCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearAllCommand { get; }

    public void MarkReportSaved(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        LastSavedReportPath = path;
    }

    private SurveyRunState CreateNewRunState() =>
        SurveyRandomizer.CreateRun(seedProvider());

    private bool CanStart() =>
        !string.IsNullOrWhiteSpace(RespondentName) &&
        !string.IsNullOrWhiteSpace(CompanyName);

    private void Start()
    {
        RespondentName = RespondentName.Trim();
        CompanyName = CompanyName.Trim();
        TrimmedRespondentName = RespondentName;
        TrimmedCompanyName = CompanyName;

        if (IsEditingIdentity)
        {
            IsEditingIdentity = false;
            ClearSavedReport();
            RefreshResults();
            Screen = AppScreen.Results;
            return;
        }

        CurrentQuestionIndex = 0;
        RebuildCurrentOptions();
        Screen = AppScreen.Question;
    }

    private void Back()
    {
        if (CurrentQuestionIndex == 0)
        {
            Screen = AppScreen.Start;
            return;
        }

        CurrentQuestionIndex--;
        RebuildCurrentOptions();
    }

    private void Next()
    {
        if (CurrentQuestionIndex < SurveyCatalog.Stages.Count - 1)
        {
            CurrentQuestionIndex++;
            RebuildCurrentOptions();
            return;
        }

        RefreshResults();
        Screen = AppScreen.Results;
    }

    private void BackToAnswers()
    {
        CurrentQuestionIndex = SurveyCatalog.Stages.Count - 1;
        RebuildCurrentOptions();
        Screen = AppScreen.Question;
    }

    private void EditIdentity()
    {
        IsEditingIdentity = true;
        Screen = AppScreen.Start;
    }

    private void Restart()
    {
        SurveyRunState = CreateNewRunState();
        RespondentName = identityDefaults.RespondentName;
        CompanyName = identityDefaults.CompanyName;
        TrimmedRespondentName = RespondentName.Trim();
        TrimmedCompanyName = CompanyName.Trim();
        IsEditingIdentity = false;
        CurrentQuestionIndex = 0;
        CurrentOptions = Array.Empty<AtomicOptionViewModel>();
        Results = Array.Empty<ResultRowViewModel>();
        StageAnswerSummaries = Array.Empty<StageAnswerSummaryViewModel>();
        TotalSelections = 0;
        HasSelections = false;
        ClearSavedReport();
        Screen = AppScreen.Start;
        NotifySelectionCount();
    }

    private void RebuildCurrentOptions()
    {
        var byId = SurveyDisplayCatalog.OptionsForStage(CurrentQuestionIndex)
            .ToDictionary(option => option.Id, StringComparer.Ordinal);
        var order = SurveyRunState.RandomizedOrderByStage[CurrentQuestionIndex];

        CurrentOptions = order
            .Select((id, displayIndex) =>
            {
                var option = byId[id];
                var decorative = DecorativePalette[displayIndex % DecorativePalette.Length];
                return new AtomicOptionViewModel(
                    option.Id,
                    option.Text,
                    decorative.Fill,
                    decorative.Border,
                    SurveyRunState.IsDisplaySelected(option),
                    selected =>
                    {
                        SurveyRunState.SetDisplaySelected(option, selected);
                        ClearSavedReport();
                        NotifySelectionCount();
                    });
            })
            .ToArray();

        NotifySelectionCount();
    }

    private void SelectAllCurrent()
    {
        foreach (var option in CurrentOptions)
        {
            option.IsSelected = true;
        }
        NotifySelectionCount();
    }

    private void ClearAllCurrent()
    {
        foreach (var option in CurrentOptions)
        {
            option.IsSelected = false;
        }
        NotifySelectionCount();
    }

    private void RefreshResults()
    {
        Results = ResultCalculator.Calculate(SurveyRunState)
            .Select(result => new ResultRowViewModel(
                result.TypeId,
                result.Name,
                result.PrimaryHex,
                result.Score,
                result.AbsolutePercent,
                result.ChartShare,
                result.Score / 700d))
            .ToArray();

        TotalSelections = SurveyCatalog.Stages.Sum(stage =>
            SurveyDisplayCatalog.OptionsForStage(stage.Index)
                .Count(SurveyRunState.IsDisplaySelected));
        HasSelections = Results.Sum(result => result.Score) > 0d;
        OnPropertyChanged(nameof(TotalSelectionsText));

        StageAnswerSummaries = SurveyCatalog.Stages
            .Select(stage =>
            {
                var byId = SurveyDisplayCatalog.OptionsForStage(stage.Index)
                    .ToDictionary(option => option.Id, StringComparer.Ordinal);
                var selectedTexts = SurveyRunState.RandomizedOrderByStage[stage.Index]
                    .Select(id => byId[id])
                    .Where(SurveyRunState.IsDisplaySelected)
                    .Select(option => option.Text)
                    .ToArray();
                return new StageAnswerSummaryViewModel(stage.Index, stage.Title, selectedTexts);
            })
            .ToArray();
    }

    private void ClearSavedReport() => LastSavedReportPath = string.Empty;

    private void NotifySelectionCount()
    {
        OnPropertyChanged(nameof(CurrentQuestionSelectedCount));
        OnPropertyChanged(nameof(CurrentQuestionSelectedCountText));
    }

    private void NotifyQuestionProperties()
    {
        OnPropertyChanged(nameof(CurrentQuestionTitle));
        OnPropertyChanged(nameof(QuestionProgressText));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(Progress1));
        OnPropertyChanged(nameof(Progress2));
        OnPropertyChanged(nameof(Progress3));
        OnPropertyChanged(nameof(Progress4));
        OnPropertyChanged(nameof(Progress5));
        OnPropertyChanged(nameof(Progress6));
        OnPropertyChanged(nameof(Progress7));
        NotifySelectionCount();
    }
}
