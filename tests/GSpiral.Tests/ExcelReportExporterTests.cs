using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using GSpiral.Domain;
using GSpiral.Services;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace GSpiral.Tests;

public sealed class ExcelReportExporterTests
{
    [Fact]
    public void Export_CreatesTwoNamedSheetsAndValidOpenXml()
    {
        WithWorkbook(new SurveyState(), (_, document) =>
        {
            var sheets = document.WorkbookPart!.Workbook.Sheets!.Elements<S.Sheet>().ToArray();
            Assert.Equal(["Выбранные опции", "Итоги"], sheets.Select(sheet => sheet.Name!.Value).ToArray());

            var errors = new OpenXmlValidator().Validate(document).ToArray();
            Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.Description)));
        });
    }

    [Fact]
    public void Export_SelectedCellUsesBrightStyleCheckmarkAndUnselectedCellUsesDimStyle()
    {
        var state = new SurveyState();
        state.SetSelected(0, CultureTypeId.Turquoise, true);

        WithWorkbook(state, (_, document) =>
        {
            var worksheet = Worksheet(document, "Выбранные опции");
            var selected = Cell(worksheet, "B6");
            var unselected = Cell(worksheet, "C6");

            Assert.StartsWith("✓ ", selected.InlineString!.Text!.Text);
            Assert.NotEqual(selected.StyleIndex!.Value, unselected.StyleIndex!.Value);
            Assert.Equal(5U, selected.StyleIndex!.Value);
            Assert.Equal(6U, unselected.StyleIndex!.Value);
        });
    }

    [Fact]
    public void Export_SummaryIsSortedAndChartExistsWhenThereAreSelections()
    {
        var state = new SurveyState();
        state.SetSelected(0, CultureTypeId.Rules, true);
        state.SetSelected(1, CultureTypeId.Rules, true);
        state.SetSelected(0, CultureTypeId.Success, true);

        WithWorkbook(state, (_, document) =>
        {
            var worksheet = Worksheet(document, "Итоги");
            Assert.Equal("Правила", Cell(worksheet, "A6").InlineString!.Text!.Text);
            Assert.Equal("2", Cell(worksheet, "B6").CellValue!.Text);
            Assert.Equal("Успех", Cell(worksheet, "A7").InlineString!.Text!.Text);

            var part = WorksheetPart(document, "Итоги");
            var drawingsPart = part.GetPartsOfType<DrawingsPart>().Single();
            Assert.Single(drawingsPart.ChartParts);
        });
    }

    [Fact]
    public void Export_ZeroSelectionsHasNoChartAndShowsExplicitMessage()
    {
        WithWorkbook(new SurveyState(), (_, document) =>
        {
            var part = WorksheetPart(document, "Итоги");
            Assert.Empty(part.GetPartsOfType<DrawingsPart>());
            Assert.Equal("Нет выбранных соответствий", Cell(part.Worksheet, "E6").InlineString!.Text!.Text);
        });
    }

    [Fact]
    public void Export_UsesExactPaletteInStylesheet()
    {
        var state = new SurveyState();
        state.SetSelected(0, CultureTypeId.Turquoise, true);

        WithWorkbook(state, (_, document) =>
        {
            var fills = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet.Fills!.Elements<S.Fill>().ToArray();
            var colors = fills
                .Select(fill => fill.PatternFill?.ForegroundColor?.Rgb?.Value)
                .Where(value => value is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var type in SurveyCatalog.Types)
            {
                Assert.Contains($"FF{type.PrimaryHex.TrimStart('#')}", colors);
                Assert.Contains($"FF{type.LightHex.TrimStart('#')}", colors);
            }
        });
    }

    private static void WithWorkbook(SurveyState state, Action<string, SpreadsheetDocument> assertion)
    {
        var directory = Path.Combine(Path.GetTempPath(), "g-spiral-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "report.xlsx");
        try
        {
            ExcelReportExporter.Export(path, "ООО Пример", new DateOnly(2026, 9, 16), state);
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

    private static WorksheetPart WorksheetPart(SpreadsheetDocument document, string name)
    {
        var sheet = document.WorkbookPart!.Workbook.Sheets!.Elements<S.Sheet>().Single(item => item.Name == name);
        return (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!);
    }

    private static S.Worksheet Worksheet(SpreadsheetDocument document, string name) => WorksheetPart(document, name).Worksheet;

    private static S.Cell Cell(S.Worksheet worksheet, string reference) =>
        worksheet.Descendants<S.Cell>().Single(cell => cell.CellReference == reference);
}
