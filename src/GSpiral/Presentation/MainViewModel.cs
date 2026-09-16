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
    private readonly RelayCommand startCommand;
    private readonly UserIdentityDefaults identityDefaults;
    private string respondentName = string.Empty;
    private string companyName = string.Empty;
    private string trimmedRespondentName = string.Empty;
    private string trimmedCompanyName = string.Empty;
    private AppScreen screen = AppScreen.Start;
    private int currentQuestionIndex;
    private IReadOnlyList<AnswerCardViewModel> answerCards = Array.Empty<AnswerCardViewModel>();
    private IReadOnlyList<ResultRowViewModel> results = Array.Empty<ResultRowViewModel>();
    private int totalSelections;
    private bool hasSelections;
    private bool isEditingIdentity;

    public MainViewModel()
        : this(UserIdentityDefaults.Detect())
    {
    }

    public MainViewModel(UserIdentityDefaults identityDefaults)
    {
        this.identityDefaults = identityDefaults ?? throw new ArgumentNullException(nameof(identityDefaults));
        respondentName = identityDefaults.RespondentName;
        companyName = identityDefaults.CompanyName;
        trimmedRespondentName = respondentName.Trim();
        trimmedCompanyName = companyName.Trim();

        SurveyState = new SurveyState();
        startCommand = new RelayCommand(_ => Start(), _ => CanStart());
        StartCommand = startCommand;
        BackCommand = new RelayCommand(_ => Back());
        NextCommand = new RelayCommand(_ => Next());
        RestartCommand = new RelayCommand(_ => Restart());
        BackToAnswersCommand = new RelayCommand(_ => BackToAnswers());
        EditIdentityCommand = new RelayCommand(_ => EditIdentity());
    }

    public SurveyState SurveyState { get; }

    public string RespondentName
    {
        get => respondentName;
        set
        {
            if (SetProperty(ref respondentName, value))
            {
                startCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IdentitySummaryText));
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

    public string CurrentQuestionTitle => SurveyCatalog.Questions[CurrentQuestionIndex].Title;
    public string QuestionProgressText => $"Вопрос {CurrentQuestionIndex + 1} из {SurveyCatalog.Questions.Count}";
    public string NextButtonText => CurrentQuestionIndex == SurveyCatalog.Questions.Count - 1 ? "Показать результаты" : "Далее →";

    public int CurrentQuestionSelectedCount =>
        SurveyCatalog.Types.Count(type => SurveyState.IsSelected(CurrentQuestionIndex, type.Id));

    public string CurrentQuestionSelectedCountText => $"Выбрано: {CurrentQuestionSelectedCount}";

    public IReadOnlyList<AnswerCardViewModel> AnswerCards
    {
        get => answerCards;
        private set => SetProperty(ref answerCards, value);
    }

    public IReadOnlyList<ResultRowViewModel> Results
    {
        get => results;
        private set => SetProperty(ref results, value);
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
            }
        }
    }

    public string StartActionText => IsEditingIdentity ? "Сохранить и вернуться к результатам" : "Начать";

    public DateOnly CurrentReportDate => DateOnly.FromDateTime(DateTime.Now);
    public string IdentitySummaryText => $"{RespondentName} · {CompanyName} · {CurrentReportDate:dd.MM.yyyy}";

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
            RefreshResults();
            Screen = AppScreen.Results;
            return;
        }

        CurrentQuestionIndex = 0;
        Screen = AppScreen.Question;
        RebuildAnswerCards();
    }

    private void Back()
    {
        if (CurrentQuestionIndex == 0)
        {
            Screen = AppScreen.Start;
            return;
        }

        CurrentQuestionIndex--;
        RebuildAnswerCards();
    }

    private void Next()
    {
        if (CurrentQuestionIndex < SurveyCatalog.Questions.Count - 1)
        {
            CurrentQuestionIndex++;
            RebuildAnswerCards();
            return;
        }

        RefreshResults();
        Screen = AppScreen.Results;
    }

    private void BackToAnswers()
    {
        CurrentQuestionIndex = SurveyCatalog.Questions.Count - 1;
        RebuildAnswerCards();
        Screen = AppScreen.Question;
    }

    private void EditIdentity()
    {
        IsEditingIdentity = true;
        Screen = AppScreen.Start;
    }

    private void Restart()
    {
        SurveyState.Reset();
        RespondentName = identityDefaults.RespondentName;
        CompanyName = identityDefaults.CompanyName;
        TrimmedRespondentName = RespondentName.Trim();
        TrimmedCompanyName = CompanyName.Trim();
        IsEditingIdentity = false;
        CurrentQuestionIndex = 0;
        AnswerCards = Array.Empty<AnswerCardViewModel>();
        Results = Array.Empty<ResultRowViewModel>();
        TotalSelections = 0;
        HasSelections = false;
        Screen = AppScreen.Start;
        NotifySelectionCount();
    }

    private void RebuildAnswerCards()
    {
        var question = SurveyCatalog.Questions[CurrentQuestionIndex];
        var questionIndex = CurrentQuestionIndex;
        AnswerCards = SurveyCatalog.Types
            .Select(type => new AnswerCardViewModel(
                type.Id,
                type.Name,
                question.Options[type.Id],
                type.PrimaryHex,
                type.LightHex,
                SurveyState.IsSelected(questionIndex, type.Id),
                selected =>
                {
                    SurveyState.SetSelected(questionIndex, type.Id, selected);
                    NotifySelectionCount();
                }))
            .ToArray();
        NotifySelectionCount();
    }

    private void RefreshResults()
    {
        Results = ResultCalculator.Calculate(SurveyState)
            .Select(x => new ResultRowViewModel(x.TypeId, x.Name, x.PrimaryHex, x.Score, x.Share, x.Score / 7d))
            .ToArray();
        TotalSelections = Results.Sum(x => x.Score);
        HasSelections = TotalSelections > 0;
    }

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
