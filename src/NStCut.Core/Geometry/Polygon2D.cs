using System;
using System.Linq;

namespace NStCut.Core.Geometry;

public sealed class Polygon2D
{
    public IReadOnlyList<Point> Vertices { get; }

    public Polygon2D(IEnumerable<Point> vertices)
    {
        Vertices = vertices.ToList().AsReadOnly();
    }

    public Rect Bounds
    {
        get
        {
            if (Vertices.Count == 0) return new Rect(0, 0, 0, 0);
            var minX = Vertices.Min(v => v.X);
            var maxX = Vertices.Max(v => v.X);
            var minY = Vertices.Min(v => v.Y);
            var maxY = Vertices.Max(v => v.Y);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }

    public double Area => Math.Abs(SignedArea);

    public double SignedArea
    {
        get
        {
            if (Vertices.Count < 3) return 0.0;
            // Compute relative to the first vertex to avoid catastrophic
            // cancellation on large absolute coordinates.
            var x0 = Vertices[0].X;
            var y0 = Vertices[0].Y;
            var sum = 0.0;
            for (var i = 0; i < Vertices.Count; i++)
            {
                var a = Vertices[i];
                var b = Vertices[(i + 1) % Vertices.Count];
                var ax = a.X - x0;
                var ay = a.Y - y0;
                var bx = b.X - x0;
                var by = b.Y - y0;
                sum += ax * by - bx * ay;
            }
            return sum * 0.5;
        }
    }

    public bool ContainsPoint(Point p)
    {
        if (Vertices.Count < 3) return false;
        var inside = false;
        for (int i = 0, j = Vertices.Count - 1; i < Vertices.Count; j = i++)
        {
            var a = Vertices[i];
            var b = Vertices[j];
            var intersects = (a.Y > p.Y) != (b.Y > p.Y) &&
                             (p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X);
            if (intersects) inside = !inside;
        }
        return inside;
    }

    public double Perimeter
    {
        get
        {
            if (Vertices.Count == 0) return 0.0;
            var sum = 0.0;
            for (var i = 0; i < Vertices.Count; i++)
            {
                var a = Vertices[i];
                var b = Vertices[(i + 1) % Vertices.Count];
                var dx = a.X - b.X;
                var dy = a.Y - b.Y;
                sum += Math.Sqrt(dx * dx + dy * dy);
            }
            return sum;
        }
    }
}
