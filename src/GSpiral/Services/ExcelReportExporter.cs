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
    private const uint FirstTypeStyle = 5;
    private const uint PercentStyle = 17;

    public static void Export(string path, string companyName, DateOnly date, SurveyState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentNullException.ThrowIfNull(state);

        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new S.Workbook();

        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = BuildStyles();
        stylesPart.Stylesheet.Save();

        var optionsPart = workbookPart.AddNewPart<WorksheetPart>();
        optionsPart.Worksheet = BuildOptionsWorksheet(companyName, date, state);
        optionsPart.Worksheet.Save();

        var results = ResultCalculator.Calculate(state);
        var resultsPart = workbookPart.AddNewPart<WorksheetPart>();
        resultsPart.Worksheet = BuildResultsWorksheet(companyName, date, results);

        if (results.Sum(x => x.Score) > 0)
        {
            AddDonutChart(resultsPart, results);
        }

        resultsPart.Worksheet.Save();

        var sheets = workbookPart.Workbook.AppendChild(new S.Sheets());
        sheets.Append(
            new S.Sheet
            {
                Id = workbookPart.GetIdOfPart(optionsPart),
                SheetId = 1U,
                Name = "Выбранные опции"
            },
            new S.Sheet
            {
                Id = workbookPart.GetIdOfPart(resultsPart),
                SheetId = 2U,
                Name = "Итоги"
            });

        workbookPart.Workbook.DefinedNames = new S.DefinedNames(
            new S.DefinedName("'Выбранные опции'!$A$1:$H$11") { Name = "_xlnm.Print_Area", LocalSheetId = 0U },
            new S.DefinedName("'Итоги'!$A$1:$J$13") { Name = "_xlnm.Print_Area", LocalSheetId = 1U });

        workbookPart.Workbook.Save();
    }

    private static S.Worksheet BuildOptionsWorksheet(string companyName, DateOnly date, SurveyState state)
    {
        var sheetData = new S.SheetData();
        var worksheet = new S.Worksheet(
            BuildSheetProperties(),
            new S.SheetViews(
                new S.SheetView
                {
                    WorkbookViewId = 0U,
                    ShowGridLines = false,
                    Pane = new S.Pane
                    {
                        HorizontalSplit = 5D,
                        VerticalSplit = 1D,
                        TopLeftCell = "B6",
                        ActivePane = S.PaneValues.BottomRight,
                        State = S.PaneStateValues.Frozen
                    }
                }),
            BuildOptionsColumns(),
            sheetData);

        var mergeCells = new S.MergeCells(
            new S.MergeCell { Reference = "A1:H1" },
            new S.MergeCell { Reference = "A2:H2" },
            new S.MergeCell { Reference = "A3:H3" });
        worksheet.Append(mergeCells);

        sheetData.Append(
            Row(1, 28, TextCell("A1", "Типология корпоративных культур", TitleStyle)),
            Row(2, 22, TextCell("A2", $"Компания: {companyName}", SubtitleStyle)),
            Row(3, 22, TextCell("A3", $"Дата: {date:dd.MM.yyyy}", SubtitleStyle)),
            Row(4, 9));

        var header = new S.Row { RowIndex = 5U, Height = 48D, CustomHeight = true };
        var headers = new[]
        {
            "Тип организации",
            "Атмосфера",
            "Система управления",
            "Убеждения",
            "Слоган",
            "Лидер",
            "Какие потребности рядового сотрудника удовлетворяет",
            "В каких условиях эффективна"
        };
        for (var i = 0; i < headers.Length; i++)
        {
            header.Append(TextCell(CellReference(i + 1, 5), headers[i], HeaderStyle));
        }
        sheetData.Append(header);

        for (var typeIndex = 0; typeIndex < SurveyCatalog.Types.Count; typeIndex++)
        {
            var type = SurveyCatalog.Types[typeIndex];
            var rowIndex = 6 + typeIndex;
            var row = new S.Row { RowIndex = (uint)rowIndex, Height = 105D, CustomHeight = true };
            row.Append(TextCell($"A{rowIndex}", type.Name, SelectedStyle(type.Id)));

            for (var questionIndex = 0; questionIndex < SurveyCatalog.Questions.Count; questionIndex++)
            {
                var selected = state.IsSelected(questionIndex, type.Id);
                var text = SurveyCatalog.Questions[questionIndex].Options[type.Id];
                if (selected)
                {
                    text = $"✓ {text}";
                }

                row.Append(TextCell(
                    CellReference(questionIndex + 2, rowIndex),
                    text,
                    selected ? SelectedStyle(type.Id) : UnselectedStyle(type.Id)));
            }

            sheetData.Append(row);
        }

        worksheet.Append(
            new S.AutoFilter { Reference = "A5:H11" },
            new S.PageMargins { Left = 0.25D, Right = 0.25D, Top = 0.45D, Bottom = 0.45D, Header = 0.2D, Footer = 0.2D },
            new S.PageSetup
            {
                Orientation = S.OrientationValues.Landscape,
                PaperSize = 9U,
                FitToWidth = 1U,
                FitToHeight = 0U
            });

        return worksheet;
    }

    private static S.Worksheet BuildResultsWorksheet(
        string companyName,
        DateOnly date,
        IReadOnlyList<CultureResult> results)
    {
        var sheetData = new S.SheetData();
        var worksheet = new S.Worksheet(
            BuildSheetProperties(),
            new S.SheetViews(new S.SheetView { WorkbookViewId = 0U, ShowGridLines = false }),
            new S.Columns(
                Column(1, 1, 24),
                Column(2, 2, 13),
                Column(3, 3, 12),
                Column(4, 4, 3),
                Column(5, 10, 13)),
            sheetData);

        var mergeCells = new S.MergeCells(
            new S.MergeCell { Reference = "A1:J1" },
            new S.MergeCell { Reference = "A2:J2" },
            new S.MergeCell { Reference = "A3:J3" });

        sheetData.Append(
            Row(1, 28, TextCell("A1", "Результаты корпоративной культуры", TitleStyle)),
            Row(2, 22, TextCell("A2", $"Компания: {companyName}", SubtitleStyle)),
            Row(3, 22, TextCell("A3", $"Дата: {date:dd.MM.yyyy}", SubtitleStyle)),
            Row(4, 9),
            Row(5, 34,
                TextCell("A5", "Тип организации", HeaderStyle),
                TextCell("B5", "Совпадений", HeaderStyle),
                TextCell("C5", "Доля", HeaderStyle)));

        var total = results.Sum(x => x.Score);
        if (total == 0)
        {
            mergeCells.Append(new S.MergeCell { Reference = "E6:J12" });
        }

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var rowIndex = 6 + i;
            var row = Row(rowIndex, 30,
                TextCell($"A{rowIndex}", result.Name, SelectedStyle(result.TypeId)),
                NumberCell($"B{rowIndex}", result.Score, BodyStyle),
                NumberCell($"C{rowIndex}", result.Share, PercentStyle));

            if (total == 0 && i == 0)
            {
                row.Append(TextCell("E6", "Нет выбранных соответствий", TitleStyle));
                row.Height = 50D;
            }

            sheetData.Append(row);
        }

        sheetData.Append(Row(13, 26,
            TextCell("A13", "Всего выбранных соответствий", HeaderStyle),
            NumberCell("B13", total, BodyStyle)));

        worksheet.Append(
            mergeCells,
            new S.PageMargins { Left = 0.35D, Right = 0.35D, Top = 0.45D, Bottom = 0.45D, Header = 0.2D, Footer = 0.2D },
            new S.PageSetup
            {
                Orientation = S.OrientationValues.Landscape,
                PaperSize = 9U,
                FitToWidth = 1U,
                FitToHeight = 1U
            });

        return worksheet;
    }

    private static void AddDonutChart(WorksheetPart worksheetPart, IReadOnlyList<CultureResult> results)
    {
        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        var chartPart = drawingsPart.AddNewPart<ChartPart>();
        chartPart.ChartSpace = BuildChartSpace(results);
        chartPart.ChartSpace.Save();

        var worksheetDrawing = new Xdr.WorksheetDrawing();
        drawingsPart.WorksheetDrawing = worksheetDrawing;
        var chartRelId = drawingsPart.GetIdOfPart(chartPart);

        var frame = new Xdr.GraphicFrame(
            new Xdr.NonVisualGraphicFrameProperties(
                new Xdr.NonVisualDrawingProperties { Id = 2U, Name = "Профиль корпоративной культуры" },
                new Xdr.NonVisualGraphicFrameDrawingProperties()),
            new Xdr.Transform(
                new A.Offset { X = 0L, Y = 0L },
                new A.Extents { Cx = 0L, Cy = 0L }),
            new A.Graphic(
                new A.GraphicData(
                    new C.ChartReference { Id = chartRelId })
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart"
                }));
        frame.Macro = string.Empty;

        var anchor = new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId("4"),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId("4"),
                new Xdr.RowOffset("0")),
            new Xdr.ToMarker(
                new Xdr.ColumnId("10"),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId("14"),
                new Xdr.RowOffset("0")),
            frame,
            new Xdr.ClientData());

        worksheetDrawing.Append(anchor);
        worksheetDrawing.Save();

        var drawing = new S.Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) };
        var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("Worksheet is required before adding the chart.");
        worksheet.Append(drawing);
    }

    private static C.ChartSpace BuildChartSpace(IReadOnlyList<CultureResult> results)
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

        var stringCache = new C.StringCache(new C.PointCount { Val = (uint)results.Count });
        for (var i = 0; i < results.Count; i++)
        {
            stringCache.Append(new C.StringPoint(new C.NumericValue(results[i].Name)) { Index = (uint)i });
        }
        series.Append(new C.CategoryAxisData(
            new C.StringReference(
                new C.Formula("'Итоги'!$A$6:$A$11"),
                stringCache)));

        var numberCache = new C.NumberingCache(
            new C.FormatCode("General"),
            new C.PointCount { Val = (uint)results.Count });
        for (var i = 0; i < results.Count; i++)
        {
            numberCache.Append(new C.NumericPoint(new C.NumericValue(results[i].Score.ToString(System.Globalization.CultureInfo.InvariantCulture))) { Index = (uint)i });
        }
        series.Append(new C.Values(
            new C.NumberReference(
                new C.Formula("'Итоги'!$B$6:$B$11"),
                numberCache)));

        var doughnut = new C.DoughnutChart(
            new C.VaryColors { Val = true },
            series,
            new C.HoleSize { Val = 62 },
            new C.FirstSliceAngle { Val = (ushort)270 });

        var title = new C.Title(
            new C.ChartText(
                new C.RichText(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(new A.Run(new A.Text("Профиль корпоративной культуры"))))),
            new C.Overlay { Val = false });

        var legend = new C.Legend(
            new C.LegendPosition { Val = C.LegendPositionValues.Right },
            new C.Layout(),
            new C.Overlay { Val = false });

        var chart = new C.Chart(
            title,
            new C.AutoTitleDeleted { Val = false },
            new C.PlotArea(new C.Layout(), doughnut),
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
            PatternFill(S.PatternValues.Solid, "FFE9EDF2"));

        foreach (var type in SurveyCatalog.Types)
        {
            fills.Append(PatternFill(S.PatternValues.Solid, Argb(type.PrimaryHex)));
            fills.Append(PatternFill(S.PatternValues.Solid, Argb(type.LightHex)));
        }
        fills.Count = (uint)fills.ChildElements.Count;

        var borders = new S.Borders(
            new S.Border(),
            ThinBorder("FFD7DEE8"))
        { Count = 2U };

        var cellStyleFormats = new S.CellStyleFormats(new S.CellFormat()) { Count = 1U };
        var cellFormats = new S.CellFormats();
        cellFormats.Append(CellFormat(0, 0, 0, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Top, true));
        cellFormats.Append(CellFormat(1, 0, 0, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Center, true));
        cellFormats.Append(CellFormat(4, 0, 0, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Center, true));
        cellFormats.Append(CellFormat(3, 2, 1, S.HorizontalAlignmentValues.Center, S.VerticalAlignmentValues.Center, true));
        cellFormats.Append(CellFormat(0, 0, 1, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Top, true));

        for (var i = 0; i < SurveyCatalog.Types.Count; i++)
        {
            var primaryFill = (uint)(3 + i * 2);
            var lightFill = primaryFill + 1U;
            cellFormats.Append(CellFormat(2, primaryFill, 1, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Top, true));
            cellFormats.Append(CellFormat(0, lightFill, 1, S.HorizontalAlignmentValues.Left, S.VerticalAlignmentValues.Top, true));
        }

        var percent = CellFormat(0, 0, 1, S.HorizontalAlignmentValues.Right, S.VerticalAlignmentValues.Center, false);
        percent.NumberFormatId = 164U;
        percent.ApplyNumberFormat = true;
        cellFormats.Append(percent);
        cellFormats.Count = (uint)cellFormats.ChildElements.Count;

        var cellStyles = new S.CellStyles(
            new S.CellStyle { Name = "Normal", FormatId = 0U, BuiltinId = 0U })
        { Count = 1U };

        return new S.Stylesheet(numberingFormats, fonts, fills, borders, cellStyleFormats, cellFormats, cellStyles);
    }

    private static S.SheetProperties BuildSheetProperties() =>
        new(new S.PageSetupProperties { FitToPage = true, AutoPageBreaks = false });

    private static S.Columns BuildOptionsColumns() =>
        new(
            Column(1, 1, 18),
            Column(2, 2, 27),
            Column(3, 3, 34),
            Column(4, 4, 33),
            Column(5, 5, 25),
            Column(6, 6, 36),
            Column(7, 7, 38),
            Column(8, 8, 38));

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

    private static S.Cell NumberCell(string reference, double value, uint styleIndex) =>
        new()
        {
            CellReference = reference,
            DataType = S.CellValues.Number,
            StyleIndex = styleIndex,
            CellValue = new S.CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };

    private static uint SelectedStyle(CultureTypeId id) => FirstTypeStyle + (uint)((int)id * 2);
    private static uint UnselectedStyle(CultureTypeId id) => SelectedStyle(id) + 1U;

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
