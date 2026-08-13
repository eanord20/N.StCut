using NStCut.Core.Geometry;

namespace NStCut.Core.Contracts;

public interface IDxfGeometryService
{
    DxfContour ReadContour(string path);
}

public sealed class DxfContour
{
    public Rect Bounds { get; set; }
    public Polygon2D? Polygon { get; set; }
}
