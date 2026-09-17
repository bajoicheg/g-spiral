using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GSpiral.Controls;
using GSpiral.Domain;

namespace GSpiral.Services;

public static class PieChartPngRenderer
{
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static byte[] Render(IReadOnlyList<SurveyScoreResult> results, int width = 900, int height = 480)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (width < 400) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 260) throw new ArgumentOutOfRangeException(nameof(height));
        if (results.Sum(result => result.Score) <= 0d)
            throw new ArgumentException("At least one positive result is required.", nameof(results));

        byte[]? bytes = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                bytes = RenderCore(results, width, height);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new InvalidOperationException("Не удалось отрисовать диаграмму для XLSX.", failure);
        return bytes ?? throw new InvalidOperationException("Диаграмма XLSX не была создана.");
    }

    private static byte[] RenderCore(IReadOnlyList<SurveyScoreResult> results, int width, int height)
    {
        var items = results.Where(result => result.Score > 0d).ToArray();
        var total = items.Sum(result => result.Score);
        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            DrawText(dc, "Структура набранных баллов", 28, 22, 26, FontWeights.SemiBold, new SolidColorBrush(Color.FromRgb(31, 41, 55)));
            DrawText(dc, "Доля каждого типа в сумме набранных баллов", 28, 58, 15, FontWeights.Normal, new SolidColorBrush(Color.FromRgb(102, 112, 133)));

            var center = new Point(width * 0.355d, height * 0.52d);
            var radiusX = width * 0.285d;
            var radiusY = Math.Min(height * 0.245d, radiusX * 0.52d);
            var depth = Math.Clamp(height * 0.06d, 20d, 30d);
            var slices = new List<(Pie3DSliceGeometry Geometry, Brush Top, Brush Side)>();
            var angle = -90d;

            foreach (var item in items)
            {
                var sweep = item.Score / total * 360d;
                var color = ParseColor(item.PrimaryHex);
                slices.Add((
                    Pie3DGeometryBuilder.BuildSlice(angle, sweep, center, radiusX, radiusY, depth),
                    new SolidColorBrush(color),
                    new SolidColorBrush(Darken(color, 0.62d))));
                angle += sweep;
            }

            var shadow = new SolidColorBrush(Color.FromArgb(28, 17, 24, 39));
            dc.DrawEllipse(shadow, null, new Point(center.X + 4, center.Y + depth + 10), radiusX * 0.92d, radiusY * 0.40d);

            var sidePen = new Pen(new SolidColorBrush(Color.FromArgb(85, 255, 255, 255)), 0.8d);
            foreach (var slice in slices)
                foreach (var side in slice.Geometry.Sides)
                    dc.DrawGeometry(slice.Side, sidePen, side);

            var topPen = new Pen(Brushes.White, 1.5d);
            foreach (var slice in slices)
                dc.DrawGeometry(slice.Top, topPen, slice.Geometry.Top);

            var legendX = width * 0.68d;
            var legendY = 100d;
            var rowHeight = 49d;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var y = legendY + i * rowHeight;
                var color = ParseColor(item.PrimaryHex);
                dc.DrawEllipse(new SolidColorBrush(color), null, new Point(legendX + 8, y + 10), 7, 7);
                DrawText(dc, item.Name, legendX + 24, y, 16, FontWeights.SemiBold, new SolidColorBrush(Color.FromRgb(55, 65, 81)));
                DrawText(dc, $"{(item.Score / total * 100d).ToString("0.0", RussianCulture)}%", legendX + 190, y, 15, FontWeights.SemiBold, new SolidColorBrush(Color.FromRgb(55, 65, 81)));
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void DrawText(DrawingContext dc, string text, double x, double y, double size, FontWeight weight, Brush brush)
    {
        var formatted = new FormattedText(
            text,
            RussianCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            brush,
            1d);
        dc.DrawText(formatted, new Point(x, y));
    }

    private static Color ParseColor(string value) => (Color)ColorConverter.ConvertFromString(value);

    private static Color Darken(Color color, double factor) =>
        Color.FromRgb(
            (byte)Math.Clamp(Math.Round(color.R * factor), 0d, 255d),
            (byte)Math.Clamp(Math.Round(color.G * factor), 0d, 255d),
            (byte)Math.Clamp(Math.Round(color.B * factor), 0d, 255d));
}
