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
        if (items.Length == 0 || total <= 0d || ActualWidth <= 20d || ActualHeight <= 20d)
        {
            return;
        }

        var depth = Math.Clamp(ActualHeight * 0.075d, 16d, 24d);
        var radiusX = Math.Max(40d, (ActualWidth - 28d) / 2d);
        var radiusY = Math.Max(28d, Math.Min(radiusX * 0.52d, (ActualHeight - depth - 30d) / 2d));
        var center = new Point(ActualWidth / 2d, (ActualHeight - depth) / 2d);
        var slices = new List<(Pie3DSliceGeometry Geometry, Brush TopBrush, Brush SideBrush)>();

        var angle = -90d;
        foreach (var item in items)
        {
            var sweep = item.Score / total * 360d;
            var color = (Color)ColorConverter.ConvertFromString(item.PrimaryHex);
            var sideColor = Darken(color, 0.64d);
            slices.Add((
                Pie3DGeometryBuilder.BuildSlice(angle, sweep, center, radiusX, radiusY, depth),
                new SolidColorBrush(color),
                new SolidColorBrush(sideColor)));
            angle += sweep;
        }

        var sidePen = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 0.8d);
        foreach (var slice in slices)
        {
            foreach (var side in slice.Geometry.Sides)
            {
                drawingContext.DrawGeometry(slice.SideBrush, sidePen, side);
            }
        }

        var topPen = new Pen(Brushes.White, 1.4d);
        foreach (var slice in slices)
        {
            drawingContext.DrawGeometry(slice.TopBrush, topPen, slice.Geometry.Top);
        }
    }

    private static Color Darken(Color color, double factor) =>
        Color.FromRgb(
            (byte)Math.Clamp(Math.Round(color.R * factor), 0d, 255d),
            (byte)Math.Clamp(Math.Round(color.G * factor), 0d, 255d),
            (byte)Math.Clamp(Math.Round(color.B * factor), 0d, 255d));
}
