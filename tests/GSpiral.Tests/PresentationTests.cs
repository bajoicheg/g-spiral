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
    public void SurveyRandomizer_DisplayOrderIsReproducibleAndEveryStageIsAPermutation()
    {
        var first = SurveyRandomizer.CreateDisplayOrders(12345);
        var second = SurveyRandomizer.CreateDisplayOrders(12345);
        var different = SurveyRandomizer.CreateDisplayOrders(54321);

        Assert.Equal(7, first.Count);
        Assert.Equal(first.SelectMany(x => x), second.SelectMany(x => x));
        Assert.NotEqual(first.SelectMany(x => x), different.SelectMany(x => x));

        for (var stage = 0; stage < SurveyCatalog.Stages.Count; stage++)
        {
            var expected = SurveyDisplayCatalog.OptionsForStage(stage).Select(x => x.Id).OrderBy(x => x).ToArray();
            var actual = first[stage].OrderBy(x => x).ToArray();
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void AtomicOptionViewModel_DoesNotExposeHiddenCultureIdentity()
    {
        var propertyNames = typeof(AtomicOptionViewModel).GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("CultureTypeId", propertyNames);
        Assert.DoesNotContain("TypeId", propertyNames);
        Assert.DoesNotContain("TypeName", propertyNames);
    }

    [Fact]
    public void MainViewModel_PrefillsIdentityAndStartsWithDeduplicatedRandomizedOptions()
    {
        var seeds = new Queue<int?>([100, 200]);
        var vm = new MainViewModel(new UserIdentityDefaults("v.vasilev", "GRADIENT"), () => seeds.Dequeue());

        Assert.Equal("v.vasilev", vm.RespondentName);
        Assert.Equal("GRADIENT", vm.CompanyName);
        Assert.True(vm.StartCommand.CanExecute(null));

        vm.StartCommand.Execute(null);

        Assert.Equal(AppScreen.Question, vm.Screen);
        Assert.Equal(0, vm.CurrentQuestionIndex);
        Assert.Equal("Атмосфера", vm.CurrentQuestionTitle);
        Assert.Equal(SurveyDisplayCatalog.OptionsForStage(0).Count, vm.CurrentOptions.Count);
        Assert.Equal(vm.CurrentOptions.Count, vm.CurrentOptions.Select(option => option.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("Выбрано: 0 из " + vm.CurrentOptions.Count, vm.CurrentQuestionSelectedCountText);
        Assert.Equal(vm.SurveyRunState.RandomizedOrderByStage[0], vm.CurrentOptions.Select(x => x.OptionId));
    }

    [Fact]
    public void MainViewModel_OneVisibleDuplicateSelectionMarksEveryLinkedHiddenAtom()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("user", "DOMAIN"), () => 123);
        vm.StartCommand.Execute(null);
        vm.NextCommand.Execute(null); // Система управления

        var regular = vm.CurrentOptions.Single(option => option.Text == "Используется регулярный менеджмент");
        var definition = SurveyDisplayCatalog.GetOption(1, regular.OptionId);
        Assert.True(definition.AtomicOptionIds.Count >= 2);

        regular.IsSelected = true;

        Assert.All(definition.AtomicOptionIds, id => Assert.True(vm.SurveyRunState.IsSelected(id)));
        Assert.Equal(1, vm.CurrentQuestionSelectedCount);
    }

    [Fact]
    public void Navigation_BackPreservesDisplayOrderAndSelections()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("user", "DOMAIN"), () => 123);
        vm.StartCommand.Execute(null);
        var order = vm.CurrentOptions.Select(x => x.OptionId).ToArray();
        vm.CurrentOptions[2].IsSelected = true;

        vm.NextCommand.Execute(null);
        vm.BackCommand.Execute(null);

        Assert.Equal(order, vm.CurrentOptions.Select(x => x.OptionId).ToArray());
        Assert.True(vm.CurrentOptions.Single(x => x.OptionId == order[2]).IsSelected);
        Assert.Equal("Выбрано: 1 из " + vm.CurrentOptions.Count, vm.CurrentQuestionSelectedCountText);
    }

    [Fact]
    public void SelectAllAndClearAllAffectOnlyCurrentStage()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("user", "DOMAIN"), () => 321);
        vm.StartCommand.Execute(null);
        vm.SelectAllCommand.Execute(null);
        Assert.All(vm.CurrentOptions, option => Assert.True(option.IsSelected));

        vm.NextCommand.Execute(null);
        Assert.All(vm.CurrentOptions, option => Assert.False(option.IsSelected));
        vm.BackCommand.Execute(null);
        Assert.All(vm.CurrentOptions, option => Assert.True(option.IsSelected));

        vm.ClearAllCommand.Execute(null);
        Assert.All(vm.CurrentOptions, option => Assert.False(option.IsSelected));
    }

    [Fact]
    public void ResultRow_RoundsScoresAndExplainsExpressionPercentage()
    {
        var row = new ResultRowViewModel(
            CultureTypeId.Success,
            "Успех",
            "#E47C22",
            241.6666667d,
            241.6666667d / 7d,
            0.20d,
            241.6666667d / 700d);

        Assert.Equal("242 / 700", row.ScoreText);
        Assert.Equal("34,5% выраженности", row.AbsolutePercentText);
        Assert.Equal("242 / 700 · 34,5% выраженности", row.ScoreAndPercentText);
    }

    [Fact]
    public void ResultsUseExpressionPercentagePieShareAndDeduplicatedAnswerSummaries()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("user", "DOMAIN"), () => 456);
        vm.StartCommand.Execute(null);
        vm.SelectAllCommand.Execute(null);
        var selectedInFirstStage = vm.CurrentOptions.Count;

        while (vm.Screen == AppScreen.Question)
        {
            vm.NextCommand.Execute(null);
        }

        Assert.Equal(AppScreen.Results, vm.Screen);
        Assert.Equal(selectedInFirstStage, vm.TotalSelections);
        Assert.Equal(6, vm.Results.Count);
        Assert.All(vm.Results, row =>
        {
            Assert.Contains("/ 700", row.ScoreText, StringComparison.Ordinal);
            Assert.EndsWith("% выраженности", row.AbsolutePercentText, StringComparison.Ordinal);
        });
        Assert.Contains("700", vm.ResultsPercentExplanation, StringComparison.Ordinal);
        Assert.Contains("сумме набранных баллов", vm.PieShareExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(7, vm.StageAnswerSummaries.Count);
        Assert.Equal("Атмосфера", vm.StageAnswerSummaries[0].Title);
        Assert.Equal(selectedInFirstStage, vm.StageAnswerSummaries[0].SelectedTexts.Count);
        Assert.Equal(
            selectedInFirstStage,
            vm.StageAnswerSummaries[0].SelectedTexts.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void EditIdentityPreservesAnswersAndRestartCreatesNewDisplayOrderAndDefaults()
    {
        var seeds = new Queue<int?>([111, 222, 333]);
        var vm = new MainViewModel(new UserIdentityDefaults("original", "DOMAIN"), () => seeds.Dequeue());
        vm.StartCommand.Execute(null);
        var firstRunOrder = vm.CurrentOptions.Select(x => x.OptionId).ToArray();
        var selectedDefinition = SurveyDisplayCatalog.GetOption(0, firstRunOrder[0]);
        vm.CurrentOptions[0].IsSelected = true;
        while (vm.Screen == AppScreen.Question)
        {
            vm.NextCommand.Execute(null);
        }

        vm.EditIdentityCommand.Execute(null);
        vm.RespondentName = "changed";
        vm.CompanyName = "NEW";
        vm.StartCommand.Execute(null);
        Assert.All(selectedDefinition.AtomicOptionIds, id => Assert.True(vm.SurveyRunState.IsSelected(id)));

        vm.RestartCommand.Execute(null);
        Assert.Equal("original", vm.RespondentName);
        Assert.Equal("DOMAIN", vm.CompanyName);
        Assert.All(selectedDefinition.AtomicOptionIds, id => Assert.False(vm.SurveyRunState.IsSelected(id)));

        vm.StartCommand.Execute(null);
        Assert.NotEqual(firstRunOrder, vm.CurrentOptions.Select(x => x.OptionId).ToArray());
    }

    [Fact]
    public void SavedReportBecomesStaleAfterAnswerChange()
    {
        var vm = new MainViewModel(new UserIdentityDefaults("user", "DOMAIN"), () => 987);
        vm.StartCommand.Execute(null);
        vm.MarkReportSaved(Path.Combine(Path.GetTempPath(), "report.xlsx"));
        Assert.True(vm.HasSavedReport);

        vm.CurrentOptions[0].IsSelected = true;
        Assert.False(vm.HasSavedReport);
    }

    [Fact]
    public void AppMetadata_ContainsAboutCopyAndCopyright()
    {
        Assert.Contains("Spiral Dynamics", AppMetadata.AboutText, StringComparison.Ordinal);
        Assert.Contains("Клэра Грейвза", AppMetadata.AboutText, StringComparison.Ordinal);
        Assert.Equal("© 2026 V. Vasilev", AppMetadata.CopyrightText);
    }

    [Fact]
    public void ReportFileName_RemainsCompatibleWithV11Pattern()
    {
        var name = ReportFileName.Build("v:vasilev", "GRADIENT/UK", new DateOnly(2026, 9, 17));
        Assert.Equal("Spiral_v_vasilev_GRADIENT_UK_2026-09-17.xlsx", name);
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
}
