using GSpiral.Domain;
using GSpiral.Services;

namespace GSpiral.Tests;

public sealed class CatalogAndScoringTests
{
    [Fact]
    public void Catalog_HasSixTypesSevenStagesFortyTwoCellsAndThreeToSixAtomicOptionsPerCell()
    {
        Assert.Equal(6, SurveyCatalog.Types.Count);
        Assert.Equal(7, SurveyCatalog.Stages.Count);
        Assert.Equal(42, SurveyCatalog.Cells.Count);
        Assert.All(SurveyCatalog.Cells, cell => Assert.InRange(cell.Options.Count, 3, 6));

        var optionIds = SurveyCatalog.Cells.SelectMany(cell => cell.Options).Select(option => option.Id).ToArray();
        Assert.Equal(optionIds.Length, optionIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(SurveyCatalog.Cells.SelectMany(cell => cell.Options), option => Assert.False(string.IsNullOrWhiteSpace(option.Text)));
    }

    [Fact]
    public void Catalog_PreservesCanonicalTypesStagesAndApprovedPalette()
    {
        Assert.Equal(
            ["Бирюза", "Возможности", "Успех", "Правила", "Сила", "Принадлежность"],
            SurveyCatalog.Types.Select(type => type.Name).ToArray());
        Assert.Equal(
            ["Атмосфера", "Система управления", "Убеждения", "Слоган", "Лидер", "Какие потребности рядового сотрудника удовлетворяет", "В каких условиях эффективна"],
            SurveyCatalog.Stages.Select(stage => stage.Title).ToArray());
        Assert.Equal("#16A6A1", SurveyCatalog.GetType(CultureTypeId.Turquoise).PrimaryHex);
        Assert.Equal("#8B5AA7", SurveyCatalog.GetType(CultureTypeId.Belonging).PrimaryHex);
    }

    [Fact]
    public void Catalog_EachStageContainsOptionsFromAllSixHiddenCells()
    {
        foreach (var stage in SurveyCatalog.Stages)
        {
            var cells = SurveyCatalog.Cells.Where(cell => cell.StageIndex == stage.Index).ToArray();
            Assert.Equal(6, cells.Length);
            Assert.Equal(6, cells.Select(cell => cell.TypeId).Distinct().Count());
            Assert.Equal(cells.Sum(cell => cell.Options.Count), SurveyCatalog.OptionsForStage(stage.Index).Count);
        }
    }

    [Fact]
    public void DisplayCatalog_DeduplicatesIdenticalTextWithinStageAndKeepsAllHiddenContributions()
    {
        var stage = SurveyCatalog.Stages.Single(stage => stage.Title == "Система управления");
        var display = SurveyDisplayCatalog.OptionsForStage(stage.Index);
        var regular = display.Single(option => option.Text == "Используется регулярный менеджмент");

        Assert.Equal(1, display.Count(option => option.Text == "Используется регулярный менеджмент"));
        Assert.Equal(2, regular.AtomicOptionIds.Count);
        Assert.Equal(
            regular.AtomicOptionIds.Count,
            regular.AtomicOptionIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            display.Count,
            display.Select(option => option.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void DisplaySelection_SelectsAllLinkedAtomsAndScoresEveryLinkedTypeIndependently()
    {
        var stage = SurveyCatalog.Stages.Single(stage => stage.Title == "Система управления");
        var regular = SurveyDisplayCatalog.OptionsForStage(stage.Index)
            .Single(option => option.Text == "Используется регулярный менеджмент");
        var run = SurveyRandomizer.CreateRun(1234);

        run.SetDisplaySelected(regular, true);

        Assert.All(regular.AtomicOptionIds, optionId => Assert.True(run.IsSelected(optionId)));
        var results = ResultCalculator.Calculate(run);
        Assert.True(results.Single(result => result.TypeId == CultureTypeId.Success).Score > 0d);
        Assert.True(results.Single(result => result.TypeId == CultureTypeId.Rules).Score > 0d);
    }

    [Fact]
    public void CellScore_UsesProportionalHundredPointWeightWithoutPrematureRounding()
    {
        var cell = new CellDefinition(
            "test-cell",
            CultureTypeId.Turquoise,
            0,
            [
                new AtomicOptionDefinition("a", "test-cell", 0, "A"),
                new AtomicOptionDefinition("b", "test-cell", 0, "B"),
                new AtomicOptionDefinition("c", "test-cell", 0, "C"),
                new AtomicOptionDefinition("d", "test-cell", 0, "D")
            ]);
        var state = new SurveyRunState();

        Assert.Equal(0d, ResultCalculator.CellScore(state, cell), 10);
        state.SetSelected("a", true);
        state.SetSelected("b", true);
        state.SetSelected("c", true);
        Assert.Equal(75d, ResultCalculator.CellScore(state, cell), 10);
        state.SetSelected("d", true);
        Assert.Equal(100d, ResultCalculator.CellScore(state, cell), 10);
    }

    [Fact]
    public void CellScore_OneOfThreeKeepsRepeatingFractionInternally()
    {
        var cell = new CellDefinition(
            "thirds",
            CultureTypeId.Rules,
            0,
            [
                new AtomicOptionDefinition("a", "thirds", 0, "A"),
                new AtomicOptionDefinition("b", "thirds", 0, "B"),
                new AtomicOptionDefinition("c", "thirds", 0, "C")
            ]);
        var state = new SurveyRunState();
        state.SetSelected("a", true);

        Assert.Equal(100d / 3d, ResultCalculator.CellScore(state, cell), 10);
    }

    [Fact]
    public void Calculate_NoSelections_ReturnsZerosInCanonicalOrder()
    {
        var results = ResultCalculator.Calculate(new SurveyRunState());

        Assert.Equal(SurveyCatalog.Types.Select(type => type.Id), results.Select(result => result.TypeId));
        Assert.All(results, result =>
        {
            Assert.Equal(0d, result.Score, 10);
            Assert.Equal(0d, result.AbsolutePercent, 10);
            Assert.Equal(0d, result.ChartShare, 10);
        });
    }

    [Fact]
    public void Calculate_SelectingAllOptionsOfOneType_ReturnsSevenHundredAndHundredPercentExpression()
    {
        var state = new SurveyRunState();
        foreach (var cell in SurveyCatalog.Cells.Where(cell => cell.TypeId == CultureTypeId.Opportunities))
        {
            foreach (var option in cell.Options)
            {
                state.SetSelected(option.Id, true);
            }
        }

        var results = ResultCalculator.Calculate(state);
        var opportunities = Assert.Single(results, result => result.TypeId == CultureTypeId.Opportunities);

        Assert.Equal(700d, opportunities.Score, 10);
        Assert.Equal(100d, opportunities.AbsolutePercent, 10);
        Assert.Equal(1d, opportunities.ChartShare, 10);
    }

    [Fact]
    public void Calculate_AllSelections_ReturnsHundredPercentExpressionForEveryType()
    {
        var state = new SurveyRunState();
        foreach (var option in SurveyCatalog.Cells.SelectMany(cell => cell.Options))
        {
            state.SetSelected(option.Id, true);
        }

        var results = ResultCalculator.Calculate(state);

        Assert.All(results, result => Assert.Equal(700d, result.Score, 10));
        Assert.Equal(4200d, results.Sum(result => result.Score), 8);
        Assert.All(results, result => Assert.Equal(100d, result.AbsolutePercent, 8));
        Assert.All(results, result => Assert.Equal(1d / 6d, result.ChartShare, 10));
    }

    [Fact]
    public void Calculate_SortsDescendingAndUsesCanonicalOrderForTies()
    {
        var state = new SurveyRunState();
        SelectWholeCell(state, CultureTypeId.Rules, 0);
        SelectWholeCell(state, CultureTypeId.Rules, 1);
        SelectWholeCell(state, CultureTypeId.Opportunities, 0);
        SelectWholeCell(state, CultureTypeId.Success, 0);

        var results = ResultCalculator.Calculate(state);

        Assert.Equal(CultureTypeId.Rules, results[0].TypeId);
        Assert.Equal(200d, results[0].Score, 10);
        Assert.Equal(CultureTypeId.Opportunities, results[1].TypeId);
        Assert.Equal(CultureTypeId.Success, results[2].TypeId);
        Assert.Equal(0.5d, results[0].ChartShare, 10);
    }

    private static void SelectWholeCell(SurveyRunState state, CultureTypeId typeId, int stageIndex)
    {
        var cell = SurveyCatalog.GetCell(typeId, stageIndex);
        foreach (var option in cell.Options)
        {
            state.SetSelected(option.Id, true);
        }
    }
}
