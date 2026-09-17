using GSpiral.Domain;
using GSpiral.Presentation;
using GSpiral.Services;

namespace GSpiral.Tests;

public sealed class PresentationTests
{
    [Fact]
    public void IdentityDefaults_DetectsWindowsUserAndDomain()
    {
        var identity = UserIdentityDefaults.Detect();

        Assert.Equal(Environment.UserName, identity.RespondentName);
        Assert.Equal(Environment.UserDomainName, identity.CompanyName);
    }

    [Fact]
    public void MainViewModel_PrefillsEditableIdentityAndRequiresBothFields()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("v.vasilev", "GRADIENT"));

        Assert.Equal("v.vasilev", vm.RespondentName);
        Assert.Equal("GRADIENT", vm.CompanyName);
        Assert.True(vm.StartCommand.CanExecute(null));

        vm.RespondentName = " ";
        Assert.False(vm.StartCommand.CanExecute(null));

        vm.RespondentName = "  Иван Иванов  ";
        vm.CompanyName = "  Gradient  ";
        vm.StartCommand.Execute(null);

        Assert.Equal(AppScreen.Question, vm.Screen);
        Assert.Equal("Иван Иванов", vm.RespondentName);
        Assert.Equal("Gradient", vm.CompanyName);
        Assert.Equal(0, vm.CurrentQuestionIndex);
        Assert.Equal(6, vm.AnswerCards.Count);
    }

    [Fact]
    public void IdentityScreenCopy_ReflectsNormalAndEditModesAndVersion()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("user", "DOMAIN"));

        Assert.Equal("Кто проходит опрос?", vm.IdentityScreenTitle);
        Assert.Equal("Имя и компания определены из Windows. При необходимости их можно исправить перед началом.", vm.IdentityScreenDescription);
        Assert.Equal("G-Spiral 1.1.0", vm.AppVersionText);

        vm.StartCommand.Execute(null);
        while (vm.Screen == AppScreen.Question)
        {
            vm.NextCommand.Execute(null);
        }
        vm.EditIdentityCommand.Execute(null);

        Assert.Equal("Изменить данные респондента", vm.IdentityScreenTitle);
        Assert.Equal("Изменения применятся к результату и следующему XLSX. Ответы сохранятся.", vm.IdentityScreenDescription);
    }

    [Fact]
    public void Navigation_AllowsZeroChoicesPreservesAnswersAndShowsSelectionCount()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("user", "DOMAIN"));
        vm.StartCommand.Execute(null);

        Assert.Equal(0, vm.CurrentQuestionSelectedCount);
        Assert.Equal("Выбрано: 0", vm.CurrentQuestionSelectedCountText);

        vm.AnswerCards[0].IsSelected = true;
        vm.AnswerCards[1].IsSelected = true;
        Assert.Equal(2, vm.CurrentQuestionSelectedCount);
        Assert.Equal("Выбрано: 2", vm.CurrentQuestionSelectedCountText);

        vm.NextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentQuestionIndex);
        Assert.Equal(0, vm.CurrentQuestionSelectedCount);

        vm.BackCommand.Execute(null);
        Assert.Equal(0, vm.CurrentQuestionIndex);
        Assert.True(vm.AnswerCards[0].IsSelected);
        Assert.True(vm.AnswerCards[1].IsSelected);
        Assert.Equal(2, vm.CurrentQuestionSelectedCount);
    }

    [Fact]
    public void EditIdentityFromResults_PreservesAnswersAndReturnsToResults()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("user", "DOMAIN"));
        vm.StartCommand.Execute(null);
        vm.AnswerCards[0].IsSelected = true;

        while (vm.Screen == AppScreen.Question)
        {
            vm.NextCommand.Execute(null);
        }

        Assert.Equal(AppScreen.Results, vm.Screen);
        Assert.Equal(1, vm.TotalSelections);

        vm.EditIdentityCommand.Execute(null);
        Assert.Equal(AppScreen.Start, vm.Screen);
        Assert.True(vm.IsEditingIdentity);
        Assert.Equal("Сохранить и вернуться к результатам", vm.StartActionText);

        vm.RespondentName = "  another.user  ";
        vm.CompanyName = "  NEWDOMAIN  ";
        vm.StartCommand.Execute(null);

        Assert.Equal(AppScreen.Results, vm.Screen);
        Assert.False(vm.IsEditingIdentity);
        Assert.Equal("another.user", vm.RespondentName);
        Assert.Equal("NEWDOMAIN", vm.CompanyName);
        Assert.True(vm.SurveyState.IsSelected(0, CultureTypeId.Turquoise));
        Assert.Equal(1, vm.TotalSelections);
    }

    [Fact]
    public void Restart_RestoresDetectedIdentityAndClearsAnswers()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("v.vasilev", "GRADIENT"));
        vm.RespondentName = "Changed User";
        vm.CompanyName = "Changed Company";
        vm.StartCommand.Execute(null);
        vm.AnswerCards[2].IsSelected = true;
        vm.MarkReportSaved(Path.Combine(Path.GetTempPath(), "old.xlsx"));
        vm.RestartCommand.Execute(null);

        Assert.Equal(AppScreen.Start, vm.Screen);
        Assert.Equal("v.vasilev", vm.RespondentName);
        Assert.Equal("GRADIENT", vm.CompanyName);
        Assert.Equal(0, vm.TotalSelections);
        Assert.False(vm.SurveyState.IsSelected(0, CultureTypeId.Success));
        Assert.False(vm.HasSavedReport);
        Assert.Equal(string.Empty, vm.LastSavedReportPath);
    }

    [Fact]
    public void SavedReportState_IsExposedAndBecomesStaleAfterAnswerChange()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("user", "DOMAIN"));
        vm.StartCommand.Execute(null);
        var path = Path.Combine(Path.GetTempPath(), "Spiral_user_DOMAIN_2026-09-16.xlsx");

        vm.MarkReportSaved(path);

        Assert.True(vm.HasSavedReport);
        Assert.Equal(path, vm.LastSavedReportPath);
        Assert.Contains(path, vm.SavedReportStatusText, StringComparison.Ordinal);

        vm.AnswerCards[0].IsSelected = true;

        Assert.False(vm.HasSavedReport);
        Assert.Equal(string.Empty, vm.LastSavedReportPath);
    }

    [Fact]
    public void ShellOpenHelper_CreatesShellLaunchesForFileAndContainingFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "g-spiral-shell-test");
        var path = Path.Combine(directory, "report.xlsx");

        var file = ShellOpenHelper.ForFile(path);
        var folder = ShellOpenHelper.ForContainingFolder(path);

        Assert.Equal(path, file.FileName);
        Assert.True(file.UseShellExecute);
        Assert.Equal(directory, folder.FileName);
        Assert.True(folder.UseShellExecute);
    }

    [Fact]
    public void ResultRow_ShowsScoreAndShareTogetherInRussianFormat()
    {
        var row = new ResultRowViewModel(CultureTypeId.Success, "Успех", "#E47C22", 2, 2d / 3d, 2d / 7d);

        Assert.Equal("2 из 7 · 66,7%", row.ScoreAndShareText);
    }

    [Fact]
    public void ReportFileName_UsesRespondentCompanyAndDateAndSanitizesInvalidCharacters()
    {
        var name = ReportFileName.Build("v:vasilev", "GRADIENT/UK", new DateOnly(2026, 9, 16));

        Assert.Equal("Spiral_v_vasilev_GRADIENT_UK_2026-09-16.xlsx", name);
    }
}
