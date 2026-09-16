using GSpiral.Presentation;
using GSpiral.Services;

namespace GSpiral.Tests;

public sealed class PresentationTests
{
    [Fact]
    public void StartCommand_RequiresCompanyNameAndTrimsIt()
    {
        var vm = new MainViewModel();
        Assert.False(vm.StartCommand.CanExecute(null));

        vm.CompanyName = "  ООО Пример  ";
        Assert.True(vm.StartCommand.CanExecute(null));

        vm.StartCommand.Execute(null);

        Assert.Equal(AppScreen.Question, vm.Screen);
        Assert.Equal("ООО Пример", vm.CompanyName);
        Assert.Equal("ООО Пример", vm.TrimmedCompanyName);
        Assert.Equal(0, vm.CurrentQuestionIndex);
        Assert.Equal(6, vm.AnswerCards.Count);
    }

    [Fact]
    public void Navigation_AllowsZeroChoicesAndPreservesAnswers()
    {
        var vm = new MainViewModel { CompanyName = "Пример" };
        vm.StartCommand.Execute(null);

        vm.AnswerCards[0].IsSelected = true;
        vm.NextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentQuestionIndex);

        vm.BackCommand.Execute(null);
        Assert.Equal(0, vm.CurrentQuestionIndex);
        Assert.True(vm.AnswerCards[0].IsSelected);

        for (var i = 0; i < 7; i++)
        {
            if (vm.Screen == AppScreen.Question)
            {
                vm.NextCommand.Execute(null);
            }
        }

        Assert.Equal(AppScreen.Results, vm.Screen);
        Assert.Equal(1, vm.TotalSelections);
    }

    [Fact]
    public void Restart_ClearsCompanyAndAnswers()
    {
        var vm = new MainViewModel { CompanyName = "Пример" };
        vm.StartCommand.Execute(null);
        vm.AnswerCards[2].IsSelected = true;
        vm.RestartCommand.Execute(null);

        Assert.Equal(AppScreen.Start, vm.Screen);
        Assert.Equal(string.Empty, vm.CompanyName);
        Assert.Equal(0, vm.TotalSelections);
        Assert.False(vm.SurveyState.IsSelected(0, Domain.CultureTypeId.Success));
    }

    [Fact]
    public void ReportFileName_SanitizesFileSystemCharactersOnlyInFilename()
    {
        var name = ReportFileName.Build("ООО:Тест", new DateOnly(2026, 9, 16));

        Assert.Equal("Корпоративная_культура_ООО_Тест_2026-09-16.xlsx", name);
    }
}
