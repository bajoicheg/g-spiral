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
    private string companyName = string.Empty;
    private string trimmedCompanyName = string.Empty;
    private AppScreen screen = AppScreen.Start;
    private int currentQuestionIndex;
    private IReadOnlyList<AnswerCardViewModel> answerCards = Array.Empty<AnswerCardViewModel>();
    private IReadOnlyList<ResultRowViewModel> results = Array.Empty<ResultRowViewModel>();
    private int totalSelections;
    private bool hasSelections;

    public MainViewModel()
    {
        SurveyState = new SurveyState();
        startCommand = new RelayCommand(_ => Start(), _ => !string.IsNullOrWhiteSpace(CompanyName));
        StartCommand = startCommand;
        BackCommand = new RelayCommand(_ => Back());
        NextCommand = new RelayCommand(_ => Next());
        RestartCommand = new RelayCommand(_ => Restart());
        BackToAnswersCommand = new RelayCommand(_ => BackToAnswers());
    }

    public SurveyState SurveyState { get; }

    public string CompanyName
    {
        get => companyName;
        set
        {
            if (SetProperty(ref companyName, value))
            {
                startCommand.RaiseCanExecuteChanged();
            }
        }
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

    public DateOnly CurrentReportDate => DateOnly.FromDateTime(DateTime.Now);

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

    private void Start()
    {
        TrimmedCompanyName = CompanyName.Trim();
        CompanyName = TrimmedCompanyName;
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

    private void Restart()
    {
        SurveyState.Reset();
        CompanyName = string.Empty;
        TrimmedCompanyName = string.Empty;
        CurrentQuestionIndex = 0;
        AnswerCards = Array.Empty<AnswerCardViewModel>();
        Results = Array.Empty<ResultRowViewModel>();
        TotalSelections = 0;
        HasSelections = false;
        Screen = AppScreen.Start;
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
                selected => SurveyState.SetSelected(questionIndex, type.Id, selected)))
            .ToArray();
    }

    private void RefreshResults()
    {
        Results = ResultCalculator.Calculate(SurveyState)
            .Select(x => new ResultRowViewModel(x.TypeId, x.Name, x.PrimaryHex, x.Score, x.Share, x.Score / 7d))
            .ToArray();
        TotalSelections = Results.Sum(x => x.Score);
        HasSelections = TotalSelections > 0;
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
    }
}
