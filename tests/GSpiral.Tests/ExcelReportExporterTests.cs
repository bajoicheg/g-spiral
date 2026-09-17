using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using GSpiral.Domain;
using GSpiral.Services;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace GSpiral.Tests;

public sealed class ExcelReportExporterTests
{
    private static readonly DateTime GeneratedAt = new(2026, 9, 17, 12, 48, 0, DateTimeKind.Local);

    [Fact]
    public void Export_CreatesThreeRequiredSheetsAndValidOpenXml()
    {
        WithWorkbook(CreateState(123), (_, document) =>
        {
            var workbook = RequireWorkbook(document);
            var sheets = (workbook.Sheets ?? throw new InvalidOperationException("Workbook must contain sheets."))
                .Elements<S.Sheet>()
                .Select(sheet => sheet.Name?.Value ?? string.Empty)
                .ToArray();

            Assert.Equal(["Выборы", "Таблица ответов", "Итоги"], sheets);
            AssertValid(document);
        });
    }

    [Fact]
    public void Export_WritesIdentityGenerationTimeVersionAndFooterToAllSheets()
    {
        WithWorkbook(CreateState(123), (_, document) =>
        {
            foreach (var sheetName in new[] { "Выборы", "Таблица ответов", "Итоги" })
            {
                var worksheet = Worksheet(document, sheetName);
                Assert.Equal("Компания: ООО Пример", Cell(worksheet, "A2").InlineString?.Text?.Text);
                Assert.Equal("Респондент: v.vasilev", Cell(worksheet, "A3").InlineString?.Text?.Text);
                Assert.Equal("Сформировано: 17.09.2026 12:48 · G-Spiral 1.2.0", Cell(worksheet, "A4").InlineString?.Text?.Text);
                var footer = worksheet.GetFirstChild<S.HeaderFooter>()
                    ?? throw new InvalidOperationException("Worksheet footer is required.");
                Assert.Equal("Сформировано G-Spiral 1.2.0", footer.OddFooter?.Text);
            }
        });
    }

    [Fact]
    public void ChoicesSheet_PreservesDisplayedRandomizedOrderWithoutCultureLabels()
    {
        var state = CreateState(777);
        var stage0Order = state.RandomizedOrderByStage[0];
        var selectedId = stage0Order[2];
        state.SetSelected(selectedId, true);

        WithWorkbook(state, (_, document) =>
        {
            var worksheet = Worksheet(document, "Выборы");
            var texts = worksheet.Descendants<S.Cell>()
                .Select(cell => cell.InlineString?.Text?.Text)
                .Where(text => text is not null)
                .Cast<string>()
                .ToArray();

            var expectedTexts = stage0Order
                .Select(id => SurveyCatalog.OptionsForStage(0).Single(option => option.Id == id).Text)
                .ToArray();
            var actualStart = Array.IndexOf(texts, expectedTexts[0]);
            Assert.True(actualStart >= 0);
            Assert.Equal(expectedTexts, texts.Skip(actualStart).Take(expectedTexts.Length).ToArray());
            Assert.DoesNotContain(SurveyCatalog.Types[0].Name, texts.Take(actualStart + expectedTexts.Length));

            var selectedText = SurveyCatalog.OptionsForStage(0).Single(option => option.Id == selectedId).Text;
            Assert.Contains("✓", texts);
            Assert.Contains(selectedText, texts);
        });
    }

    [Fact]
    public void AuditSheet_ContainsFortyTwoCellsWithMarkersAndCorrectCellScore()
    {
        var state = CreateState(100);
        var cell = SurveyCatalog.GetCell(CultureTypeId.Turquoise, 0);
        state.SetSelected(cell.Options[0].Id, true);

        WithWorkbook(state, (_, document) =>
        {
            var worksheet = Worksheet(document, "Таблица ответов");
            var auditCells = worksheet.Descendants<S.Cell>()
                .Where(c => c.CellReference?.Value is string r && IsAuditMatrixReference(r))
                .ToArray();
            Assert.Equal(42, auditCells.Length);

            var turquoiseAtmosphere = Cell(worksheet, "B7").InlineString?.Text?.Text ?? string.Empty;
            Assert.Contains("33,3 / 100", turquoiseAtmosphere, StringComparison.Ordinal);
            Assert.Contains("✓ " + cell.Options[0].Text, turquoiseAtmosphere, StringComparison.Ordinal);
            Assert.Contains("○ " + cell.Options[1].Text, turquoiseAtmosphere, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ResultsSheet_IsSortedAndContainsValidThreeDPieWithCanonicalColors()
    {
        var state = CreateState(321);
        SelectWholeCell(state, CultureTypeId.Rules, 0);
        SelectWholeCell(state, CultureTypeId.Rules, 1);
        SelectWholeCell(state, CultureTypeId.Success, 0);

        WithWorkbook(state, (_, document) =>
        {
            var worksheet = Worksheet(document, "Итоги");
            Assert.Equal("Правила", Cell(worksheet, "B7").InlineString?.Text?.Text);
            Assert.Equal("200", Cell(worksheet, "C7").CellValue?.Text);
            Assert.Equal("700", Cell(worksheet, "D7").CellValue?.Text);
            Assert.Equal(200d / 4200d, double.Parse(Cell(worksheet, "E7").CellValue?.Text ?? "0", System.Globalization.CultureInfo.InvariantCulture), 10);

            var part = WorksheetPart(document, "Итоги");
            var drawingsPart = part.GetPartsOfType<DrawingsPart>().Single();
            var chartPart = Assert.Single(drawingsPart.ChartParts);
            var chartSpace = chartPart.ChartSpace ?? throw new InvalidOperationException("Chart space required.");
            Assert.Single(chartSpace.Descendants<C.Pie3DChart>());

            var actualColors = chartSpace.Descendants<C.DataPoint>()
                .Select(point => point.Descendants<A.RgbColorModelHex>().Single().Val?.Value ?? string.Empty)
                .ToArray();
            var expectedColors = ResultCalculator.Calculate(state)
                .Select(result => result.PrimaryHex.TrimStart('#'))
                .ToArray();
            Assert.Equal(expectedColors, actualColors);
            AssertValid(document);
        });
    }

    [Fact]
    public void ZeroSelections_HasNoChartAndExplicitMessage()
    {
        WithWorkbook(CreateState(55), (_, document) =>
        {
            var part = WorksheetPart(document, "Итоги");
            Assert.Empty(part.GetPartsOfType<DrawingsPart>());
            var worksheet = part.Worksheet ?? throw new InvalidOperationException("Results sheet required.");
            Assert.Contains(
                "Нет выбранных соответствий",
                worksheet.Descendants<S.Cell>().Select(cell => cell.InlineString?.Text?.Text).OfType<string>());
            AssertValid(document);
        });
    }

    [Fact]
    public void StylesheetContainsCanonicalTypePalette()
    {
        WithWorkbook(CreateState(42), (_, document) =>
        {
            var styles = (RequireWorkbookPart(document).WorkbookStylesPart
                ?? throw new InvalidOperationException("Styles part required."))
                .Stylesheet ?? throw new InvalidOperationException("Stylesheet required.");
            var colors = (styles.Fills ?? throw new InvalidOperationException("Fills required."))
                .Elements<S.Fill>()
                .Select(fill => fill.PatternFill?.ForegroundColor?.Rgb?.Value)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var type in SurveyCatalog.Types)
            {
                Assert.Contains($"FF{type.PrimaryHex.TrimStart('#')}", colors);
                Assert.Contains($"FF{type.LightHex.TrimStart('#')}", colors);
            }
        });
    }

    private static SurveyRunState CreateState(int seed)
    {
        var state = new SurveyRunState(SurveyRandomizer.CreateOrders(seed));
        return state;
    }

    private static void SelectWholeCell(SurveyRunState state, CultureTypeId typeId, int stageIndex)
    {
        foreach (var option in SurveyCatalog.GetCell(typeId, stageIndex).Options)
        {
            state.SetSelected(option.Id, true);
        }
    }

    private static bool IsAuditMatrixReference(string reference)
    {
        var column = reference[0];
        if (column is < 'B' or > 'H')
        {
            return false;
        }
        return int.TryParse(reference[1..], out var row) && row is >= 7 and <= 12;
    }

    private static void WithWorkbook(SurveyRunState state, Action<string, SpreadsheetDocument> assertion)
    {
        var directory = Path.Combine(Path.GetTempPath(), "g-spiral-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "report.xlsx");
        try
        {
            ExcelReportExporter.Export(path, "v.vasilev", "ООО Пример", GeneratedAt, state);
            Assert.True(File.Exists(path));
            using var document = SpreadsheetDocument.Open(path, false);
            assertion(path, document);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static void AssertValid(SpreadsheetDocument document)
    {
        var errors = new OpenXmlValidator().Validate(document).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.Description)));
    }

    private static WorkbookPart RequireWorkbookPart(SpreadsheetDocument document) =>
        document.WorkbookPart ?? throw new InvalidOperationException("Workbook part is required.");

    private static S.Workbook RequireWorkbook(SpreadsheetDocument document) =>
        RequireWorkbookPart(document).Workbook ?? throw new InvalidOperationException("Workbook root is required.");

    private static WorksheetPart WorksheetPart(SpreadsheetDocument document, string name)
    {
        var workbookPart = RequireWorkbookPart(document);
        var sheets = RequireWorkbook(document).Sheets ?? throw new InvalidOperationException("Sheets required.");
        var sheet = sheets.Elements<S.Sheet>().Single(item => item.Name?.Value == name);
        return (WorksheetPart)workbookPart.GetPartById(sheet.Id?.Value ?? throw new InvalidOperationException("Relationship required."));
    }

    private static S.Worksheet Worksheet(SpreadsheetDocument document, string name) =>
        WorksheetPart(document, name).Worksheet ?? throw new InvalidOperationException($"Worksheet '{name}' required.");

    private static S.Cell Cell(S.Worksheet worksheet, string reference) =>
        worksheet.Descendants<S.Cell>().Single(cell => cell.CellReference?.Value == reference);
}
