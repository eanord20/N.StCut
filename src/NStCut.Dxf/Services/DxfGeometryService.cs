using netDxf;
using netDxf.Entities;
using NStCut.Core.Contracts;
using NStCut.Core.Geometry;

namespace NStCut.Dxf.Services;

public sealed class DxfGeometryService : IDxfGeometryService
{
    public DxfContour ReadContour(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("DXF not found.", path);

        var doc = DxfDocument.Load(path);
        var bounds = new Rect(0, 0, 0, 0);
        bool hasBounds = false;

        foreach (var entity in doc.Entities.All)
        {
            switch (entity)
            {
                case Polyline2D poly:
                    foreach (var v in poly.Vertexes)
                    {
                        AddPoint(ref bounds, ref hasBounds, v.Position.X, v.Position.Y);
                    }
                    break;

                case Line line:
                    AddPoint(ref bounds, ref hasBounds, line.StartPoint.X, line.StartPoint.Y);
                    AddPoint(ref bounds, ref hasBounds, line.EndPoint.X, line.EndPoint.Y);
                    break;

                case Arc arc:
                    var arcBounds = ArcBounds(arc);
                    AddRect(ref bounds, ref hasBounds, arcBounds);
                    break;

                case Circle circle:
                    AddPoint(ref bounds, ref hasBounds, circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius);
                    AddPoint(ref bounds, ref hasBounds, circle.Center.X + circle.Radius, circle.Center.Y + circle.Radius);
                    break;
            }
        }

        return new DxfContour
        {
            Bounds = hasBounds ? bounds : new Rect(0, 0, 0, 0),
            Polygon = null // polygonal contour will be added in v1.1
        };
    }

    private static Rect ArcBounds(Arc arc)
    {
        // Simplified: sample the arc between its start and end angle.
        const int samples = 12;
        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        double startAngle = arc.StartAngle;
        double endAngle = arc.EndAngle;
        if (endAngle < startAngle)
            endAngle += 360.0;

        for (int i = 0; i <= samples; i++)
        {
            double angle = startAngle + (endAngle - startAngle) * i / samples;
            double rad = Math.PI * angle / 180.0;
            double x = arc.Center.X + arc.Radius * Math.Cos(rad);
            double y = arc.Center.Y + arc.Radius * Math.Sin(rad);

            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static void AddPoint(ref Rect bounds, ref bool hasBounds, double x, double y)
    {
        if (!hasBounds)
        {
            bounds = new Rect(x, y, 0, 0);
            hasBounds = true;
            return;
        }

        var minX = Math.Min(bounds.Left, x);
        var maxX = Math.Max(bounds.Right, x);
        var minY = Math.Min(bounds.Bottom, y);
        var maxY = Math.Max(bounds.Top, y);
        bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static void AddRect(ref Rect bounds, ref bool hasBounds, Rect rect)
    {
        AddPoint(ref bounds, ref hasBounds, rect.Left, rect.Bottom);
        AddPoint(ref bounds, ref hasBounds, rect.Right, rect.Top);
    }
}
