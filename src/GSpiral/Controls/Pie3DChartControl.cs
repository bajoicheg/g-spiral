using System.Windows;
using System.Windows.Media;
using GSpiral.Presentation;

namespace GSpiral.Controls;

public sealed class Pie3DChartControl : FrameworkElement
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items),
        typeof(IEnumerable<ResultRowViewModel>),
        typeof(Pie3DChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<ResultRowViewModel>? Items
    {
        get => (IEnumerable<ResultRowViewModel>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var items = Items?.Where(item => item.Score > 0d).ToArray() ?? [];
        var total = items.Sum(item => item.Score);
        if (items.Length == 0 || total <= 0d || ActualWidth <= 20 || ActualHeight <= 20)
        {
            return;
        }

        var depth = Math.Min(18d, ActualHeight * 0.07d);
        var width = Math.Max(40d, ActualWidth - 20d);
        var height = Math.Max(28d, (ActualHeight - depth - 20d) * 0.62d);
        var center = new Point(ActualWidth / 2d, (ActualHeight - depth) / 2d);
        var radiusX = width / 2d;
        var radiusY = height / 2d;

        var angle = -90d;
        foreach (var item in items)
        {
            var sweep = item.Score / total * 360d;
            var color = (Color)ColorConverter.ConvertFromString(item.PrimaryHex);
            var sideColor = Color.FromRgb(
                (byte)(color.R * 0.68),
                (byte)(color.G * 0.68),
                (byte)(color.B * 0.68));
            DrawSlice(drawingContext, center with { Y = center.Y + depth }, radiusX, radiusY, angle, sweep, new SolidColorBrush(sideColor));
            angle += sweep;
        }

        angle = -90d;
        foreach (var item in items)
        {
            var sweep = item.Score / total * 360d;
            var color = (Color)ColorConverter.ConvertFromString(item.PrimaryHex);
            DrawSlice(drawingContext, center, radiusX, radiusY, angle, sweep, new SolidColorBrush(color));
            angle += sweep;
        }
    }

    private static void DrawSlice(
        DrawingContext drawingContext,
        Point center,
        double radiusX,
        double radiusY,
        double startAngle,
        double sweepAngle,
        Brush fill)
    {
        if (sweepAngle >= 359.999d)
        {
            drawingContext.DrawEllipse(fill, new Pen(Brushes.White, 1.2), center, radiusX, radiusY);
            return;
        }

        var start = PointOnEllipse(center, radiusX, radiusY, startAngle);
        var end = PointOnEllipse(center, radiusX, radiusY, startAngle + sweepAngle);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(center, true, true);
            context.LineTo(start, true, false);
            context.ArcTo(
                end,
                new Size(radiusX, radiusY),
                0d,
                sweepAngle > 180d,
                SweepDirection.Clockwise,
                true,
                false);
        }
        geometry.Freeze();
        drawingContext.DrawGeometry(fill, new Pen(Brushes.White, 1.2), geometry);
    }

    private static Point PointOnEllipse(Point center, double radiusX, double radiusY, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new Point(
            center.X + radiusX * Math.Cos(radians),
            center.Y + radiusY * Math.Sin(radians));
    }
}
