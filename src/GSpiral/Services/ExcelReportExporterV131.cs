using System.IO;
using DocumentFormat.OpenXml.Packaging;
using GSpiral.Domain;
using A = DocumentFormat.OpenXml.Drawing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace GSpiral.Services;

public static class ExcelReportExporterV131
{
    public static void Export(
        string path,
        string respondentName,
        string companyName,
        DateTime generatedAt,
        SurveyRunState state)
    {
        ExcelReportExporter.Export(path, respondentName, companyName, generatedAt, state);

        var results = ResultCalculator.Calculate(state);
        if (results.Sum(result => result.Score) <= 0d)
        {
            return;
        }

        ReplaceNativeChartWithPng(path, results);
    }

    private static void ReplaceNativeChartWithPng(string path, IReadOnlyList<SurveyScoreResult> results)
    {
        var png = PieChartPngRenderer.Render(results);

        using var document = SpreadsheetDocument.Open(path, true);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidOperationException("Workbook part is required.");
        var workbook = workbookPart.Workbook
            ?? throw new InvalidOperationException("Workbook is required.");
        var sheets = workbook.Sheets
            ?? throw new InvalidOperationException("Workbook sheets are required.");
        var resultsSheet = sheets.Elements<S.Sheet>()
            .Single(sheet => sheet.Name?.Value == "Итоги");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(
            resultsSheet.Id?.Value ?? throw new InvalidOperationException("Results sheet relationship is required."));
        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidOperationException("Results worksheet is required.");

        foreach (var drawing in worksheet.Elements<S.Drawing>().ToArray())
        {
            drawing.Remove();
        }
        foreach (var oldDrawingsPart in worksheetPart.GetPartsOfType<DrawingsPart>().ToArray())
        {
            worksheetPart.DeletePart(oldDrawingsPart);
        }

        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing();
        var imagePart = drawingsPart.AddImagePart(ImagePartType.Png);
        using (var imageStream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
        {
            imageStream.Write(png, 0, png.Length);
        }

        var imageRelationshipId = drawingsPart.GetIdOfPart(imagePart);
        var picture = new Xdr.Picture(
            new Xdr.NonVisualPictureProperties(
                new Xdr.NonVisualDrawingProperties
                {
                    Id = 2U,
                    Name = "Структура набранных баллов"
                },
                new Xdr.NonVisualPictureDrawingProperties(
                    new A.PictureLocks { NoChangeAspect = true })),
            new Xdr.BlipFill(
                new A.Blip
                {
                    Embed = imageRelationshipId,
                    CompressionState = A.BlipCompressionValues.Print
                },
                new A.Stretch(new A.FillRectangle())),
            new Xdr.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 0L, Y = 0L },
                    new A.Extents { Cx = 0L, Cy = 0L }),
                new A.PresetGeometry(new A.AdjustValueList())
                {
                    Preset = A.ShapeTypeValues.Rectangle
                }));

        var anchor = new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId("6"),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId("5"),
                new Xdr.RowOffset("0")),
            new Xdr.ToMarker(
                new Xdr.ColumnId("11"),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId("16"),
                new Xdr.RowOffset("0")),
            picture,
            new Xdr.ClientData());

        drawingsPart.WorksheetDrawing.Append(anchor);
        drawingsPart.WorksheetDrawing.Save();
        worksheet.Append(new S.Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
        worksheet.Save();
    }
}
