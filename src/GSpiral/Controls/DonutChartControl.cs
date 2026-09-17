using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GSpiral.Presentation;

namespace GSpiral.Controls;

public sealed class DonutChartControl : FrameworkElement
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items),
        typeof(IReadOnlyList<ResultRowViewModel>),
        typeof(DonutChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<ResultRowViewModel>? Items
    {
        get => (IReadOnlyList<ResultRowViewModel>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (Items is null || Items.Count == 0)
        {
            return;
        }

        var total = Items.Sum(x => x.Score);
        if (total <= 0)
        {
            return;
        }

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 20)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var outerRadius = Math.Max(10, size / 2 - 6);
        var innerRadius = outerRadius * 0.58;
        var startAngle = -90d;

        foreach (var item in Items.Where(x => x.Score > 0))
        {
            var sweep = item.Score / (double)total * 360d;
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.PrimaryHex));
            brush.Freeze();

            if (sweep >= 359.999d)
            {
                var ring = new CombinedGeometry(
                    GeometryCombineMode.Exclude,
                    new EllipseGeometry(center, outerRadius, outerRadius),
                    new EllipseGeometry(center, innerRadius, innerRadius));
                drawingContext.DrawGeometry(brush, null, ring);
            }
            else
            {
                drawingContext.DrawGeometry(brush, null, CreateSector(center, innerRadius, outerRadius, startAngle, sweep));
            }

            startAngle += sweep;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var totalText = new FormattedText(
            total.ToString(CultureInfo.InvariantCulture),
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            28,
            Brushes.Black,
            dpi);
        drawingContext.DrawText(totalText, new Point(center.X - totalText.Width / 2, center.Y - totalText.Height / 2));
    }

    private static Geometry CreateSector(Point center, double innerRadius, double outerRadius, double startAngle, double sweep)
    {
        var endAngle = startAngle + sweep;
        var outerStart = PointOnCircle(center, outerRadius, startAngle);
        var outerEnd = PointOnCircle(center, outerRadius, endAngle);
        var innerEnd = PointOnCircle(center, innerRadius, endAngle);
        var innerStart = PointOnCircle(center, innerRadius, startAngle);
        var isLargeArc = sweep > 180d;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(outerStart, isFilled: true, isClosed: true);
            context.ArcTo(outerEnd, new Size(outerRadius, outerRadius), 0, isLargeArc, SweepDirection.Clockwise, isStroked: false, isSmoothJoin: true);
            context.LineTo(innerEnd, isStroked: false, isSmoothJoin: true);
            context.ArcTo(innerStart, new Size(innerRadius, innerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, isStroked: false, isSmoothJoin: true);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
