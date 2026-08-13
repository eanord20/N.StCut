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
}
