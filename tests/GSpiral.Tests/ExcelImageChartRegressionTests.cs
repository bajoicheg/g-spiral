using DocumentFormat.OpenXml.Packaging;
using GSpiral.Domain;
using GSpiral.Services;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace GSpiral.Tests;

public sealed class ExcelImageChartRegressionTests
{
    [Fact]
    public void ResultsSheet_EmbedsRenderablePngPictureForPieChart()
    {
        var state = SurveyRandomizer.CreateRun(131);
        foreach (var option in SurveyCatalog.GetCell(CultureTypeId.Opportunities, 0).Options)
        {
            state.SetSelected(option.Id, true);
        }
        foreach (var option in SurveyCatalog.GetCell(CultureTypeId.Turquoise, 0).Options.Take(2))
        {
            state.SetSelected(option.Id, true);
        }

        var directory = Path.Combine(Path.GetTempPath(), "g-spiral-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "report.xlsx");

        try
        {
            ExcelReportExporter.Export(path, "user", "DOMAIN", new DateTime(2026, 9, 17, 17, 0, 0), state);
            using var document = SpreadsheetDocument.Open(path, false);
            var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook part required.");
            var sheet = workbookPart.Workbook.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>()
                .Single(item => item.Name?.Value == "Итоги");
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
            var drawingsPart = Assert.Single(worksheetPart.GetPartsOfType<DrawingsPart>());

            var imagePart = Assert.Single(drawingsPart.ImageParts);
            Assert.Equal("image/png", imagePart.ContentType);
            using var stream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
            Span<byte> signature = stackalloc byte[8];
            Assert.Equal(8, stream.Read(signature));
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, signature.ToArray());
            Assert.True(stream.Length > 10_000, "Embedded chart PNG should contain real rendered chart content.");

            var drawing = drawingsPart.WorksheetDrawing ?? throw new InvalidOperationException("Worksheet drawing required.");
            var picture = Assert.Single(drawing.Descendants<Xdr.Picture>());
            Assert.Contains("Структура набранных баллов", picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name?.Value ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
