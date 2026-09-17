using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using GSpiral.Domain;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace GSpiral.Services;

public static class ExcelReportExporter
{
    private const uint TitleStyle = 1;
    private const uint SubtitleStyle = 2;
    private const uint HeaderStyle = 3;
    private const uint BodyStyle = 4;
    private const uint ChoiceSelectedStyle = 5;
    private const uint ChoiceUnselectedStyle = 6;
    private const uint FirstTypeStyle = 7;
    private const uint PercentStyle = 19;
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static void Export(
        string path,
        string respondentName,
        string companyName,
        DateTime generatedAt,
        SurveyRunState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(respondentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentNullException.ThrowIfNull(state);
        ValidateRunOrder(state);

        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new S.Workbook();

        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = BuildStyles();
        stylesPart.Stylesheet.Save();

        var choicesPart = workbookPart.AddNewPart<WorksheetPart>();
        choicesPart.Worksheet = BuildChoicesWorksheet(respondentName, companyName, generatedAt, state);
        choicesPart.Worksheet.Save();

        var auditPart = workbookPart.AddNewPart<WorksheetPart>();
        auditPart.Worksheet = BuildAuditWorksheet(respondentName, companyName, generatedAt, state);
        auditPart.Worksheet.Save();

        var results = ResultCalculator.Calculate(state);
        var resultsPart = workbookPart.AddNewPart<WorksheetPart>();
        resultsPart.Worksheet = BuildResultsWorksheet(respondentName, companyName, generatedAt, results);
        if (results.Sum(result => result.Score) > 0d)
        {
            AddThreeDPieChart(resultsPart, results);
        }
        resultsPart.Worksheet.Save();

        var sheets = workbookPart.Workbook.AppendChild(new S.Sheets());
        sheets.Append(
            Sheet(workbookPart, choicesPart, 1U, "Выборы"),
            Sheet(workbookPart, auditPart, 2U, "Таблица ответов"),
            Sheet(workbookPart, resultsPart, 3U, "Итоги"));

        workbookPart.Workbook.Save();
    }

    private static void ValidateRunOrder(SurveyRunState state)
    {
        if (state.RandomizedOrderByStage.Count != SurveyCatalog.Stages.Count)
        {
            throw new InvalidOperationException("Survey run does not contain a randomized order for all stages.");
        }
    }

    private static S.Sheet Sheet(WorkbookPart workbookPart, WorksheetPart part, uint id, string name) =>
        new()
        {
            Id = workbookPart.GetIdOfPart(part),
            SheetId = id,
            Name = name
        };

    private static S.Worksheet BuildChoicesWorksheet(
        string respondentName,
        string companyName,
        DateTime generatedAt,
        SurveyRunState state)
    {
        var data = new S.SheetData();
        var worksheet = new S.Worksheet(
            BuildSheetProperties(),
            new S.SheetViews(new S.SheetView
            {
                WorkbookViewId = 0U,
                ShowGridLines = false,
                Pane = new S.Pane
                {
                    HorizontalSplit = 5D,
                    TopLeftCell = "A6",
                    ActivePane = S.PaneValues.BottomLeft,
                    State = S.PaneStateValues.Frozen
                }
            }),
            new S.Columns(
                Column(1, 1, 6),
                Column(2, 2, 88),
                Column(3, 3, 17)),
            data);

        var merges = new S.MergeCells(
            new S.MergeCell { Reference = "A1:C1" },
            new S.MergeCell { Reference = "A2:C2" },
            new S.MergeCell { Reference = "A3:C3" },
            new S.MergeCell { Reference = "A4:C4" });

        AppendMetadata(data, "Выборы пользователя", respondentName, companyName, generatedAt);
        data.Append(Row(5, 18, TextCell("A5", "✓", ChoiceSelectedStyle), TextCell("B5", "выбранный вариант", SubtitleStyle)));

        var rowIndex = 6;
        foreach (var stage in SurveyCatalog.Stages)
        {
            var selectedCount = state.RandomizedOrderByStage[stage.Index].Count(state.IsSelected);
            data.Append(Row(rowIndex, 27,
                TextCell($"A{rowIndex}", $"{stage.Index + 1}", HeaderStyle),
                TextCell($"B{rowIndex}", stage.Title, HeaderStyle),
                TextCell($"C{rowIndex}", $"Выбрано: {selectedCount}", HeaderStyle)));
            rowIndex++;

            var lookup = SurveyCatalog.OptionsForStage(stage.Index)
                .ToDictionary(option => option.Id, StringComparer.Ordinal);
            foreach (var optionId in state.RandomizedOrderByStage[stage.Index])
            {
                var option = lookup[optionId];
                var selected = state.IsSelected(option.Id);
                data.Append(Row(rowIndex, 31,
                    PlainStringCell($"A{rowIndex}", selected ? "✓" : ""),
                    TextCell($"B{rowIndex}", option.Text, selected ? ChoiceSelectedStyle : ChoiceUnselectedStyle)));
                rowIndex++;
            }

            rowIndex++;
        }

        worksheet.Append(
            merges,
            BuildPageMargins(),
            new S.PageSetup { Orientation = S.OrientationValues.Portrait, PaperSize = 9U, FitToWidth = 1U, FitToHeight = 0U },
            BuildFooter());
        return worksheet;
    }

    private static S.Worksheet BuildAuditWorksheet(
        string respondentName,
        string companyName,
        DateTime generatedAt,
        SurveyRunState state)
    {
        var data = new S.SheetData();
        var worksheet = new S.Worksheet(
            BuildSheetProperties(),
            new S.SheetViews(new S.SheetView
            {
                WorkbookViewId = 0U,
                ShowGridLines = false,
                Pane = new S.Pane
                {
                    HorizontalSplit = 6D,
                    VerticalSplit = 1D,
                    TopLeftCell = "B7",
                    ActivePane = S.PaneValues.BottomRight,
                    State = S.PaneStateValues.Frozen
                }
            }),
            BuildAuditColumns(),
            data);

        var merges = new S.MergeCells(
            new S.MergeCell { Reference = "A1:H1" },
            new S.MergeCell { Reference = "A2:H2" },
            new S.MergeCell { Reference = "A3:H3" },
            new S.MergeCell { Reference = "A4:H4" });

        AppendMetadata(data, "Таблица ответов — скрытая матрица 6 × 7", respondentName, companyName, generatedAt);
        data.Append(Row(5, 9));

        var header = new S.Row { RowIndex = 6U, Height = 48D, CustomHeight = true };
        header.Append(TextCell("A6", "Тип организации", HeaderStyle));
        for (var stageIndex = 0; stageIndex < SurveyCatalog.Stages.Count; stageIndex++)
        {
            header.Append(TextCell(CellReference(stageIndex + 2, 6), SurveyCatalog.Stages[stageIndex].Title, HeaderStyle));
        }
        data.Append(header);

        for (var typeIndex = 0; typeIndex < SurveyCatalog.Types.Count; typeIndex++)
        {
            var type = SurveyCatalog.Types[typeIndex];
            var rowIndex = 7 + typeIndex;
            var row = new S.Row { RowIndex = (uint)rowIndex, Height = 150D, CustomHeight = true };
            row.Append(TextCell($"A{rowIndex}", type.Name, TypePrimaryStyle(type.Id)));

            for (var stageIndex = 0; stageIndex < SurveyCatalog.Stages.Count; stageIndex++)
            {
                var cell = SurveyCatalog.GetCell(type.Id, stageIndex);
                var score = ResultCalculator.CellScore(state, cell);
                var lines = new List<string>
                {
                    $"{score.ToString("0.0", RussianCulture)} / 100"
                };
                lines.AddRange(cell.Options.Select(option => $"{(state.IsSelected(option.Id) ? "✓" : "○")} {option.Text}"));
                row.Append(TextCell(
                    CellReference(stageIndex + 2, rowIndex),
                    string.Join(Environment.NewLine, lines),
                    TypeLightStyle(type.Id)));
            }
            data.Append(row);
        }

        worksheet.Append(
            merges,
            BuildPageMargins(),
            new S.PageSetup { Orientation = S.OrientationValues.Landscape, PaperSize = 9U, FitToWidth = 1U, FitToHeight = 0U },
            BuildFooter());
        return worksheet;
    }

    private static S.Worksheet BuildResultsWorksheet(
        string respondentName,
        string companyName,
        DateTime generatedAt,
        IReadOnlyList<SurveyScoreResult> results)
    {
        var data = new S.SheetData();
        var worksheet = new S.Worksheet(
            BuildSheetProperties(),
            new S.SheetViews(new S.SheetView { WorkbookViewId = 0U, ShowGridLines = false }),
            new S.Columns(
                Column(1, 1, 8),
                Column(2, 2, 24),
                Column(3, 3, 14),
                Column(4, 4, 12),
                Column(5, 5, 17),
                Column(6, 11, 13)),
            data);

        var merges = new S.MergeCells(
            new S.MergeCell { Reference = "A1:K1" },
            new S.MergeCell { Reference = "A2:K2" },
            new S.MergeCell { Reference = "A3:K3" },
            new S.MergeCell { Reference = "A4:K4" });

        AppendMetadata(data, "Итоги корпоративной культуры", respondentName, companyName, generatedAt);
        data.Append(Row(5, 9));
        data.Append(Row(6, 34,
            TextCell("A6", "Место", HeaderStyle),
            TextCell("B6", "Тип организации", HeaderStyle),
            TextCell("C6", "Баллы", HeaderStyle),
            TextCell("D6", "Максимум", HeaderStyle),
            TextCell("E6", "Итоговый процент", HeaderStyle)));

        var totalScore = results.Sum(result => result.Score);
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var rowIndex = 7 + i;
            data.Append(Row(rowIndex, 32,
                NumberCell($"A{rowIndex}", i + 1, BodyStyle),
                TextCell($"B{rowIndex}", result.Name, TypePrimaryStyle(result.TypeId)),
                NumberCell($"C{rowIndex}", result.Score, BodyStyle),
                NumberCell($"D{rowIndex}", 700d, BodyStyle),
                NumberCell($"E{rowIndex}", result.AbsolutePercent / 100d, PercentStyle)));
        }

        if (totalScore <= 0d)
        {
            merges.Append(new S.MergeCell { Reference = "G7:K11" });
            data.Append(Row(14, 40, TextCell("G14", "Нет выбранных соответствий", TitleStyle)));
        }

        worksheet.Append(
            merges,
            BuildPageMargins(),
            new S.PageSetup { Orientation = S.OrientationValues.Landscape, PaperSize = 9U, FitToWidth = 1U, FitToHeight = 1U },
            BuildFooter());
        return worksheet;
    }

    private static void AppendMetadata(
        S.SheetData data,
        string title,
        string respondentName,
        string companyName,
        DateTime generatedAt)
    {
        data.Append(
            Row(1, 30, TextCell("A1", title, TitleStyle)),
            Row(2, 21, TextCell("A2", $"Компания: {companyName}", SubtitleStyle)),
            Row(3, 21, TextCell("A3", $"Респондент: {respondentName}", SubtitleStyle)),
            Row(4, 20, TextCell("A4", GeneratedMetadata(generatedAt), SubtitleStyle)));
    }

    private static string GeneratedMetadata(DateTime generatedAt) =>
        $"Сформировано: {generatedAt.ToString("dd.MM.yyyy HH:mm", RussianCulture)} · {AppMetadata.ProductName} {AppMetadata.Version}";

    private static S.HeaderFooter BuildFooter() => new(new S.OddFooter(AppMetadata.FooterText));

    private static S.PageMargins BuildPageMargins() =>
        new() { Left = 0.3D, Right = 0.3D, Top = 0.45D, Bottom = 0.45D, Header = 0.2D, Footer = 0.2D };

    private static void AddThreeDPieChart(WorksheetPart worksheetPart, IReadOnlyList<SurveyScoreResult> results)
    {
        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        var chartPart = drawingsPart.AddNewPart<ChartPart>();
        chartPart.ChartSpace = BuildThreeDPieChartSpace(results);
        chartPart.ChartSpace.Save();

        drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing();
        var chartRelId = drawingsPart.GetIdOfPart(chartPart);
        var frame = new Xdr.GraphicFrame(
            new Xdr.NonVisualGraphicFrameProperties(
                new Xdr.NonVisualDrawingProperties { Id = 2U, Name = "Структура выбранных соответствий" },
                new Xdr.NonVisualGraphicFrameDrawingProperties()),
            new Xdr.Transform(
                new A.Offset { X = 0L, Y = 0L },
                new A.Extents { Cx = 0L, Cy = 0L }),
            new A.Graphic(
                new A.GraphicData(new C.ChartReference { Id = chartRelId })
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart"
                }));
        frame.Macro = string.Empty;

        var anchor = new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId("6"), new Xdr.ColumnOffset("0"),
                new Xdr.RowId("5"), new Xdr.RowOffset("0")),
            new Xdr.ToMarker(
                new Xdr.ColumnId("11"), new Xdr.ColumnOffset("0"),
                new Xdr.RowId("15"), new Xdr.RowOffset("0")),
            frame,
            new Xdr.ClientData());
        drawingsPart.WorksheetDrawing.Append(anchor);
        drawingsPart.WorksheetDrawing.Save();

        var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("Worksheet is required before adding chart.");
        worksheet.Append(new S.Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
    }

    private static C.ChartSpace BuildThreeDPieChartSpace(IReadOnlyList<SurveyScoreResult> results)
    {
        var series = new C.PieChartSeries(
            new C.Index { Val = 0U },
            new C.Order { Val = 0U });

        for (var i = 0; i < results.Count; i++)
        {
            series.Append(new C.DataPoint(
                new C.Index { Val = (uint)i },
                new C.ChartShapeProperties(
                    new A.SolidFill(new A.RgbColorModelHex { Val = results[i].PrimaryHex.TrimStart('#') }))));
        }

        var categories = new C.StringCache(new C.PointCount { Val = (uint)results.Count });
        for (var i = 0; i < results.Count; i++)
        {
            categories.Append(new C.StringPoint(new C.NumericValue(results[i].Name)) { Index = (uint)i });
        }
        series.Append(new C.CategoryAxisData(
            new C.StringReference(
                new C.Formula("'Итоги'!$B$7:$B$12"),
                categories)));

        var values = new C.NumberingCache(
            new C.FormatCode("General"),
            new C.PointCount { Val = (uint)results.Count });
        for (var i = 0; i < results.Count; i++)
        {
            values.Append(new C.NumericPoint(
                new C.NumericValue(results[i].Score.ToString(CultureInfo.InvariantCulture))) { Index = (uint)i });
        }
        series.Append(new C.Values(
            new C.NumberReference(
                new C.Formula("'Итоги'!$C$7:$C$12"),
                values)));

        var pie = new C.Pie3DChart(
            new C.VaryColors { Val = true },
            series);

        var title = new C.Title(
            new C.ChartText(
                new C.RichText(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(new A.Run(new A.Text("Структура выбранных соответствий"))))),
            new C.Overlay { Val = false });
        var legend = new C.Legend(
            new C.LegendPosition { Val = C.LegendPositionValues.Right },
            new C.Layout(),
            new C.Overlay { Val = false });
        var chart = new C.Chart(
            title,
            new C.AutoTitleDeleted { Val = false },
            new C.PlotArea(new C.Layout(), pie),
            legend,
            new C.PlotVisibleOnly { Val = true },
            new C.DisplayBlanksAs { Val = C.DisplayBlanksAsValues.Zero });

        return new C.ChartSpace(
            new C.Date1904 { Val = false },
            new C.EditingLanguage { Val = "ru-RU" },
            new C.RoundedCorners { Val = false },
            chart);
    }

    private static S.Stylesheet BuildStyles()
    {
        var numberingFormats = new S.NumberingFormats(
            new S.NumberingFormat { NumberFormatId = 164U, FormatCode = "0.0%" })
        { Count = 1U };

        var fonts = new S.Fonts(
            Font("FF303846", 11, false),
            Font("FF1F2937", 18, true),
            Font("FFFFFFFF", 11, true),
            Font("FF374151", 11, true),
            Font("FF667085", 11, false))
        { Count = 5U };

        var fills = new S.Fills(
            PatternFill(S.PatternValues.None, null),
            PatternFill(S.PatternValues.Gray125, null),
            PatternFill(S.PatternValues.Solid, "FFE9EDF2"),
            PatternFill(S.PatternValues.Solid, "FF4F46E5"),
            PatternFill(S.PatternValues.Solid, "FFF2F4F7"));
        foreach (var type in SurveyCatalog.Types)
        {
            fills.Append(PatternFill(S.PatternValues.Solid, Argb(type.PrimaryHex)));
            fills.Append(PatternFill(S.PatternValues.Solid, Argb(type.LightHex)));
        }
        fills.Count = (uint)fills.ChildElements.Count;

        var borders = new S.Borders(new S.Border(), ThinBorder("FFD7DEE8")) { Count = 2U };
        var cellStyleFormats = new S.CellStyleFormats(new S.CellFormat()) { Count = 1U };
        var formats = new S.CellFormats();
        formats.Append(CellFormat(0, 0, 0, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Top, true));
        formats.Append(CellFormat(1, 0, 0, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Center, true));
        formats.Append(CellFormat(4, 0, 0, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Center, true));
        formats.Append(CellFormat(3, 2, 1, S.HorizontalAlignmentValues.Center, S.VerticalAlignmentValues.Center, true));
        formats.Append(CellFormat(0, 0, 1, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Top, true));
        formats.Append(CellFormat(2, 3, 1, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Center, true));
        formats.Append(CellFormat(0, 4, 1, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Center, true));

        for (var i = 0; i < SurveyCatalog.Types.Count; i++)
        {
            var primaryFill = (uint)(5 + i * 2);
            formats.Append(CellFormat(2, primaryFill, 1, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Top, true));
            formats.Append(CellFormat(0, primaryFill + 1U, 1, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Top, true));
        }

        var percent = CellFormat(0, 0, 1, S.HorizontalAlignmentValues.Right, S.VerticalAlignmentValues.Center, false);
        percent.NumberFormatId = 164U;
        percent.ApplyNumberFormat = true;
        formats.Append(percent);
        formats.Count = (uint)formats.ChildElements.Count;

        var styles = new S.CellStyles(new S.CellStyle { Name = "Normal", FormatId = 0U, BuiltinId = 0U }) { Count = 1U };
        return new S.Stylesheet(numberingFormats, fonts, fills, borders, cellStyleFormats, formats, styles);
    }

    private static S.SheetProperties BuildSheetProperties() =>
        new(new S.PageSetupProperties { FitToPage = true, AutoPageBreaks = false });

    private static S.Columns BuildAuditColumns() =>
        new(
            Column(1, 1, 18),
            Column(2, 2, 34),
            Column(3, 3, 39),
            Column(4, 4, 36),
            Column(5, 5, 31),
            Column(6, 6, 39),
            Column(7, 7, 39),
            Column(8, 8, 39));

    private static S.Column Column(uint min, uint max, double width) =>
        new() { Min = min, Max = max, Width = width, CustomWidth = true };

    private static S.Row Row(int index, double height, params S.Cell[] cells)
    {
        var row = new S.Row { RowIndex = (uint)index, Height = height, CustomHeight = true };
        row.Append(cells);
        return row;
    }

    private static S.Cell TextCell(string reference, string value, uint styleIndex) =>
        new()
        {
            CellReference = reference,
            DataType = S.CellValues.InlineString,
            StyleIndex = styleIndex,
            InlineString = new S.InlineString(new S.Text(value) { Space = SpaceProcessingModeValues.Preserve })
        };

    private static S.Cell PlainStringCell(string reference, string value) =>
        new()
        {
            CellReference = reference,
            DataType = S.CellValues.String,
            StyleIndex = BodyStyle,
            CellValue = new S.CellValue(value)
        };

    private static S.Cell NumberCell(string reference, double value, uint styleIndex) =>
        new()
        {
            CellReference = reference,
            DataType = S.CellValues.Number,
            StyleIndex = styleIndex,
            CellValue = new S.CellValue(value.ToString(CultureInfo.InvariantCulture))
        };

    private static uint TypePrimaryStyle(CultureTypeId id) => FirstTypeStyle + (uint)((int)id * 2);
    private static uint TypeLightStyle(CultureTypeId id) => TypePrimaryStyle(id) + 1U;
    private static string CellReference(int column, int row) => $"{ColumnName(column)}{row}";

    private static string ColumnName(int column)
    {
        var name = string.Empty;
        while (column > 0)
        {
            column--;
            name = (char)('A' + column % 26) + name;
            column /= 26;
        }
        return name;
    }

    private static S.Font Font(string rgb, double size, bool bold)
    {
        var font = new S.Font(
            new S.FontSize { Val = size },
            new S.Color { Rgb = rgb },
            new S.FontName { Val = "Aptos" },
            new S.FontFamilyNumbering { Val = 2 });
        if (bold)
        {
            font.PrependChild(new S.Bold());
        }
        return font;
    }

    private static S.Fill PatternFill(S.PatternValues pattern, string? foregroundRgb)
    {
        var patternFill = new S.PatternFill { PatternType = pattern };
        if (foregroundRgb is not null)
        {
            patternFill.ForegroundColor = new S.ForegroundColor { Rgb = foregroundRgb };
            patternFill.BackgroundColor = new S.BackgroundColor { Indexed = 64U };
        }
        return new S.Fill(patternFill);
    }

    private static S.Border ThinBorder(string rgb) =>
        new(
            new S.LeftBorder(new S.Color { Rgb = rgb }) { Style = S.BorderStyleValues.Thin },
            new S.RightBorder(new S.Color { Rgb = rgb }) { Style = S.BorderStyleValues.Thin },
            new S.TopBorder(new S.Color { Rgb = rgb }) { Style = S.BorderStyleValues.Thin },
            new S.BottomBorder(new S.Color { Rgb = rgb }) { Style = S.BorderStyleValues.Thin },
            new S.DiagonalBorder());

    private static S.CellFormat CellFormat(
        uint fontId,
        uint fillId,
        uint borderId,
        S.HorizontalAlignmentValues horizontal,
        S.VerticalAlignmentValues vertical,
        bool wrap)
    {
        var format = new S.CellFormat
        {
            FontId = fontId,
            FillId = fillId,
            BorderId = borderId,
            ApplyFont = true,
            ApplyFill = true,
            ApplyBorder = true,
            ApplyAlignment = true
        };
        format.Append(new S.Alignment
        {
            Horizontal = horizontal,
            Vertical = vertical,
            WrapText = wrap
        });
        return format;
    }

    private static string Argb(string hex) => $"FF{hex.TrimStart('#').ToUpperInvariant()}";
}
