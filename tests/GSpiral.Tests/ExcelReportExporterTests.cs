using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using GSpiral.Domain;
using GSpiral.Services;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace GSpiral.Tests;

public sealed class ExcelReportExporterTests
{
    private static readonly DateTime GeneratedAt = new(2026, 9, 17, 14, 12, 0, DateTimeKind.Local);

    [Fact]
    public void Export_CreatesV13SheetsInRequiredOrderAndValidOpenXml()
    {
        WithWorkbook(CreateState(123), (_, document) =>
        {
            var sheets = (RequireWorkbook(document).Sheets ?? throw new InvalidOperationException("Workbook must contain sheets."))
                .Elements<S.Sheet>()
                .Select(sheet => sheet.Name?.Value ?? string.Empty)
                .ToArray();

            Assert.Equal(["Таблица ответов", "Итоги", "Детализация ответов"], sheets);
            AssertValid(document);
        });
    }

    [Fact]
    public void Export_WritesIdentityGenerationTimeVersionAndFooterToAllSheets()
    {
        WithWorkbook(CreateState(123), (_, document) =>
        {
            foreach (var sheetName in new[] { "Таблица ответов", "Итоги", "Детализация ответов" })
            {
                var worksheet = Worksheet(document, sheetName);
                Assert.Equal("Компания: ООО Пример", Cell(worksheet, "A2").InlineString?.Text?.Text);
                Assert.Equal("Респондент: v.vasilev", Cell(worksheet, "A3").InlineString?.Text?.Text);
                Assert.Equal($"Сформировано: 17.09.2026 14:12 · G-Spiral {AppMetadata.Version}", Cell(worksheet, "A4").InlineString?.Text?.Text);
                var footer = worksheet.GetFirstChild<S.HeaderFooter>() ?? throw new InvalidOperationException("Worksheet footer is required.");
                Assert.Equal(AppMetadata.FooterText, footer.OddFooter?.Text);
            }
        });
    }

    [Fact]
    public void AuditSheet_ContainsFortyTwoCellsMarkersAndRoundedCellScore()
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
            Assert.Contains("33 / 100", turquoiseAtmosphere, StringComparison.Ordinal);
            Assert.DoesNotContain("33,3 / 100", turquoiseAtmosphere, StringComparison.Ordinal);
            Assert.Contains("✓ " + cell.Options[0].Text, turquoiseAtmosphere, StringComparison.Ordinal);
            Assert.Contains("○ " + cell.Options[1].Text, turquoiseAtmosphere, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AuditSheet_FrozenPaneUsesExcelCompatibleSplitCoordinatesAndSelection()
    {
        WithWorkbook(CreateState(100), (_, document) =>
        {
            var view = RequireSingleView(Worksheet(document, "Таблица ответов"));
            var pane = view.GetFirstChild<S.Pane>() ?? throw new InvalidOperationException("Frozen pane required.");
            Assert.Equal(1d, pane.HorizontalSplit?.Value);
            Assert.Equal(6d, pane.VerticalSplit?.Value);
            Assert.Equal("B7", pane.TopLeftCell?.Value);
            Assert.Equal(S.PaneValues.BottomRight, pane.ActivePane?.Value);
            Assert.Equal(S.PaneStateValues.Frozen, pane.State?.Value);
            Assert.Contains(view.Elements<S.Selection>(), selection => selection.Pane?.Value == S.PaneValues.BottomRight);
        });
    }

    [Fact]
    public void ResultsSheet_IsSortedUsesRoundedScoreAndExpressionPercentAndEmbedsRenderedPngPie()
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
            Assert.Equal(200d / 700d, DoubleCell(worksheet, "E7"), 10);

            var part = WorksheetPart(document, "Итоги");
            var drawingsPart = Assert.Single(part.GetPartsOfType<DrawingsPart>());
            Assert.Empty(drawingsPart.ChartParts);
            var imagePart = Assert.Single(drawingsPart.GetPartsOfType<ImagePart>());
            Assert.Equal("image/png", imagePart.ContentType);
            using var image = imagePart.GetStream(FileMode.Open, FileAccess.Read);
            var signature = new byte[8];
            Assert.Equal(8, image.Read(signature, 0, signature.Length));
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, signature);
            Assert.Single(drawingsPart.WorksheetDrawing?.Descendants<Xdr.Picture>() ?? []);
            AssertValid(document);
        });
    }

    [Fact]
    public void ResultsSheet_RoundsRepeatingScoresOnlyForDisplayedNumericScore()
    {
        var state = CreateState(4);
        var cell = SurveyCatalog.GetCell(CultureTypeId.Success, 0);
        state.SetSelected(cell.Options[0].Id, true);

        WithWorkbook(state, (_, document) =>
        {
            var worksheet = Worksheet(document, "Итоги");
            var result = ResultCalculator.Calculate(state).Single(r => r.TypeId == CultureTypeId.Success);
            var resultRow = FindResultRow(worksheet, "Успех");
            Assert.Equal(
                Math.Round(result.Score, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture),
                Cell(worksheet, $"C{resultRow}").CellValue?.Text);
            Assert.Equal(result.AbsolutePercent / 100d, DoubleCell(worksheet, $"E{resultRow}"), 10);
        });
    }

    [Fact]
    public void DetailSheet_IsThreeLevelHierarchyEndingInNumericZeroOrOne()
    {
        var state = CreateState(9);
        var selected = SurveyCatalog.GetCell(CultureTypeId.Turquoise, 0).Options[0];
        state.SetSelected(selected.Id, true);

        WithWorkbook(state, (_, document) =>
        {
            var worksheet = Worksheet(document, "Детализация ответов");
            Assert.Equal("Этап", Cell(worksheet, "A5").InlineString?.Text?.Text);
            Assert.Equal("Тип", Cell(worksheet, "B5").InlineString?.Text?.Text);
            Assert.Equal("Утверждение", Cell(worksheet, "C5").InlineString?.Text?.Text);
            Assert.Equal("Ответ", Cell(worksheet, "D5").InlineString?.Text?.Text);

            var rows = worksheet.Descendants<S.Row>().Where(row => (row.RowIndex?.Value ?? 0) > 5).ToArray();
            Assert.Contains(rows, row => row.OutlineLevel?.Value == 0 && RowText(row, "A") == "Атмосфера");
            Assert.Contains(rows, row => row.OutlineLevel?.Value == 1 && RowText(row, "B") == "Бирюза");

            var atomRows = rows.Where(row => row.OutlineLevel?.Value == 2).ToArray();
            Assert.NotEmpty(atomRows);
            Assert.All(atomRows, row =>
            {
                Assert.False(string.IsNullOrWhiteSpace(RowText(row, "C")));
                var answer = RowNumeric(row, "D");
                Assert.True(answer is 0d or 1d);
                Assert.False(row.Hidden?.Value ?? false);
                Assert.False(row.Collapsed?.Value ?? false);
            });

            var selectedRow = atomRows.Single(row => RowText(row, "C") == selected.Text);
            Assert.Equal(1d, RowNumeric(selectedRow, "D"));
        });
    }

    [Fact]
    public void DetailSheet_FrozenHeaderUsesVerticalSplitAndMatchingSelection()
    {
        WithWorkbook(CreateState(8), (_, document) =>
        {
            var view = RequireSingleView(Worksheet(document, "Детализация ответов"));
            var pane = view.GetFirstChild<S.Pane>() ?? throw new InvalidOperationException("Frozen pane required.");
            Assert.Null(pane.HorizontalSplit);
            Assert.Equal(5d, pane.VerticalSplit?.Value);
            Assert.Equal("A6", pane.TopLeftCell?.Value);
            Assert.Equal(S.PaneValues.BottomLeft, pane.ActivePane?.Value);
            Assert.Equal(S.PaneStateValues.Frozen, pane.State?.Value);
            Assert.Contains(view.Elements<S.Selection>(), selection => selection.Pane?.Value == S.PaneValues.BottomLeft);
        });
    }

    [Fact]
    public void ZeroSelections_HasNoDrawingAndExplicitMessage()
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
            var styles = (RequireWorkbookPart(document).WorkbookStylesPart ?? throw new InvalidOperationException("Styles part required."))
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

    private static SurveyRunState CreateState(int seed) => SurveyRandomizer.CreateRun(seed);

    private static void SelectWholeCell(SurveyRunState state, CultureTypeId typeId, int stageIndex)
    {
        foreach (var option in SurveyCatalog.GetCell(typeId, stageIndex).Options) state.SetSelected(option.Id, true);
    }

    private static bool IsAuditMatrixReference(string reference)
    {
        var column = reference[0];
        return column is >= 'B' and <= 'H' && int.TryParse(reference[1..], out var row) && row is >= 7 and <= 12;
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
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
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

    private static S.SheetView RequireSingleView(S.Worksheet worksheet) =>
        Assert.Single(worksheet.GetFirstChild<S.SheetViews>()?.Elements<S.SheetView>() ?? []);

    private static double DoubleCell(S.Worksheet worksheet, string reference) =>
        double.Parse(Cell(worksheet, reference).CellValue?.Text ?? "0", CultureInfo.InvariantCulture);

    private static int FindResultRow(S.Worksheet worksheet, string typeName)
    {
        var cell = worksheet.Descendants<S.Cell>()
            .Single(c => c.CellReference?.Value?.StartsWith('B') == true && c.InlineString?.Text?.Text == typeName);
        return int.Parse(cell.CellReference!.Value![1..], CultureInfo.InvariantCulture);
    }

    private static string RowText(S.Row row, string column) =>
        row.Elements<S.Cell>().SingleOrDefault(cell => cell.CellReference?.Value?.StartsWith(column, StringComparison.Ordinal) == true)
            ?.InlineString?.Text?.Text ?? string.Empty;

    private static double RowNumeric(S.Row row, string column)
    {
        var text = row.Elements<S.Cell>().Single(cell => cell.CellReference?.Value?.StartsWith(column, StringComparison.Ordinal) == true).CellValue?.Text ?? "0";
        return double.Parse(text, CultureInfo.InvariantCulture);
    }
}
