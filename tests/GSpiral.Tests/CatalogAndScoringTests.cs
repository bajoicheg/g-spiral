using GSpiral.Domain;
using GSpiral.Services;

namespace GSpiral.Tests;

public sealed class CatalogAndScoringTests
{
    [Fact]
    public void Catalog_HasSixTypesSevenQuestionsAndFortyTwoOptions()
    {
        Assert.Equal(6, SurveyCatalog.Types.Count);
        Assert.Equal(7, SurveyCatalog.Questions.Count);
        Assert.All(SurveyCatalog.Questions, question => Assert.Equal(6, question.Options.Count));
        Assert.Equal(42, SurveyCatalog.Questions.Sum(question => question.Options.Count));
    }

    [Fact]
    public void Catalog_UsesApprovedNamesColorsAndEditedText()
    {
        Assert.Equal(
            ["Бирюза", "Возможности", "Успех", "Правила", "Сила", "Принадлежность"],
            SurveyCatalog.Types.Select(type => type.Name).ToArray());
        Assert.Equal("#16A6A1", SurveyCatalog.GetType(CultureTypeId.Turquoise).PrimaryHex);
        Assert.Equal("#DDF3F1", SurveyCatalog.GetType(CultureTypeId.Turquoise).LightHex);
        Assert.Contains("весёлый", SurveyCatalog.Questions[4].Options[CultureTypeId.Turquoise]);
        Assert.Contains("организационная структура", SurveyCatalog.Questions[1].Options[CultureTypeId.Opportunities]);
        Assert.Contains("Документ", SurveyCatalog.Questions[2].Options[CultureTypeId.Rules]);
        Assert.Equal("Потому что война", SurveyCatalog.Questions[3].Options[CultureTypeId.Power]);
        Assert.Contains("Приобщённость", SurveyCatalog.Questions[5].Options[CultureTypeId.Belonging]);
    }

    [Fact]
    public void Calculate_NoSelections_ReturnsZerosInCanonicalOrder()
    {
        var results = ResultCalculator.Calculate(new SurveyState());

        Assert.Equal(SurveyCatalog.Types.Select(type => type.Id), results.Select(result => result.TypeId));
        Assert.All(results, result =>
        {
            Assert.Equal(0, result.Score);
            Assert.Equal(0d, result.Share);
        });
    }

    [Fact]
    public void Calculate_AllSelections_ReturnsSevenForEveryType()
    {
        var state = new SurveyState();
        for (var question = 0; question < 7; question++)
        {
            foreach (var type in SurveyCatalog.Types)
            {
                state.SetSelected(question, type.Id, true);
            }
        }

        var results = ResultCalculator.Calculate(state);

        Assert.All(results, result => Assert.Equal(7, result.Score));
        Assert.Equal(42, results.Sum(result => result.Score));
        Assert.All(results, result => Assert.Equal(1d / 6d, result.Share, 10));
    }

    [Fact]
    public void Calculate_SortsDescendingAndUsesCanonicalOrderForTies()
    {
        var state = new SurveyState();
        state.SetSelected(0, CultureTypeId.Rules, true);
        state.SetSelected(1, CultureTypeId.Rules, true);
        state.SetSelected(0, CultureTypeId.Opportunities, true);
        state.SetSelected(1, CultureTypeId.Success, true);

        var results = ResultCalculator.Calculate(state);

        Assert.Equal(CultureTypeId.Rules, results[0].TypeId);
        Assert.Equal(2, results[0].Score);
        Assert.Equal(CultureTypeId.Opportunities, results[1].TypeId);
        Assert.Equal(CultureTypeId.Success, results[2].TypeId);
        Assert.Equal(0.5d, results[0].Share, 10);
    }

    [Fact]
    public void SurveyState_PreservesSelectionsUntilReset()
    {
        var state = new SurveyState();
        state.SetSelected(6, CultureTypeId.Power, true);
        Assert.True(state.IsSelected(6, CultureTypeId.Power));

        state.Reset();

        Assert.False(state.IsSelected(6, CultureTypeId.Power));
    }
}
