using System.Windows;
using System.Windows.Media;

namespace GSpiral.Controls;

public sealed record Pie3DSliceGeometry(Geometry Top, IReadOnlyList<Geometry> Sides);

public static class Pie3DGeometryBuilder
{
    private const double Epsilon = 0.0001d;

    public static Pie3DSliceGeometry BuildSlice(
        double startAngle,
        double sweepAngle,
        Point center,
        double radiusX,
        double radiusY,
        double depth)
    {
        if (radiusX <= 0d) throw new ArgumentOutOfRangeException(nameof(radiusX));
        if (radiusY <= 0d) throw new ArgumentOutOfRangeException(nameof(radiusY));
        if (depth <= 0d) throw new ArgumentOutOfRangeException(nameof(depth));
        if (sweepAngle <= 0d || sweepAngle > 360d + Epsilon) throw new ArgumentOutOfRangeException(nameof(sweepAngle));

        var top = sweepAngle >= 360d - Epsilon
            ? Frozen(new EllipseGeometry(center, radiusX, radiusY))
            : BuildSector(center, radiusX, radiusY, startAngle, sweepAngle);

        var sides = new List<Geometry>();
        var endAngle = startAngle + Math.Min(sweepAngle, 360d);
        AddVisibleOuterFaces(sides, center, radiusX, radiusY, depth, startAngle, endAngle);

        if (sweepAngle < 360d - Epsilon)
        {
            if (IsLowerHalf(startAngle)) sides.Add(BuildRadialFace(center, radiusX, radiusY, depth, startAngle));
            if (IsLowerHalf(endAngle)) sides.Add(BuildRadialFace(center, radiusX, radiusY, depth, endAngle));
        }

        return new Pie3DSliceGeometry(top, sides);
    }

    private static void AddVisibleOuterFaces(
        ICollection<Geometry> sides,
        Point center,
        double radiusX,
        double radiusY,
        double depth,
        double startAngle,
        double endAngle)
    {
        var firstTurn = (int)Math.Floor(startAngle / 360d) - 1;
        var lastTurn = (int)Math.Ceiling(endAngle / 360d) + 1;
        for (var turn = firstTurn; turn <= lastTurn; turn++)
        {
            var visibleStart = turn * 360d;
            var visibleEnd = visibleStart + 180d;
            var a = Math.Max(startAngle, visibleStart);
            var b = Math.Min(endAngle, visibleEnd);
            if (b - a > Epsilon)
            {
                sides.Add(BuildOuterFace(center, radiusX, radiusY, depth, a, b));
            }
        }
    }

    private static Geometry BuildSector(Point center, double radiusX, double radiusY, double startAngle, double sweepAngle)
    {
        var start = PointOnEllipse(center, radiusX, radiusY, startAngle);
        var end = PointOnEllipse(center, radiusX, radiusY, startAngle + sweepAngle);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(center, true, true);
            context.LineTo(start, true, false);
            context.ArcTo(end, new Size(radiusX, radiusY), 0d, sweepAngle > 180d, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildOuterFace(Point center, double radiusX, double radiusY, double depth, double startAngle, double endAngle)
    {
        var bottomCenter = new Point(center.X, center.Y + depth);
        var topStart = PointOnEllipse(center, radiusX, radiusY, startAngle);
        var topEnd = PointOnEllipse(center, radiusX, radiusY, endAngle);
        var bottomStart = PointOnEllipse(bottomCenter, radiusX, radiusY, startAngle);
        var bottomEnd = PointOnEllipse(bottomCenter, radiusX, radiusY, endAngle);
        var sweep = endAngle - startAngle;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(topStart, true, true);
            context.ArcTo(topEnd, new Size(radiusX, radiusY), 0d, sweep > 180d, SweepDirection.Clockwise, true, false);
            context.LineTo(bottomEnd, true, false);
            context.ArcTo(bottomStart, new Size(radiusX, radiusY), 0d, sweep > 180d, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildRadialFace(Point center, double radiusX, double radiusY, double depth, double angle)
    {
        var bottomCenter = new Point(center.X, center.Y + depth);
        var topOuter = PointOnEllipse(center, radiusX, radiusY, angle);
        var bottomOuter = PointOnEllipse(bottomCenter, radiusX, radiusY, angle);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(center, true, true);
            context.LineTo(topOuter, true, false);
            context.LineTo(bottomOuter, true, false);
            context.LineTo(bottomCenter, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static bool IsLowerHalf(double angle)
    {
        var normalized = angle % 360d;
        if (normalized < 0d) normalized += 360d;
        return normalized > Epsilon && normalized < 180d - Epsilon;
    }

    private static Point PointOnEllipse(Point center, double radiusX, double radiusY, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new Point(center.X + radiusX * Math.Cos(radians), center.Y + radiusY * Math.Sin(radians));
    }

    private static Geometry Frozen(Geometry geometry)
    {
        geometry.Freeze();
        return geometry;
    }
}
