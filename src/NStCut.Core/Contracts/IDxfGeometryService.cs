using System.Linq;

using NStCut.Core.Geometry;

namespace NStCut.Core.Contracts;

public interface IDxfGeometryService
{
    DxfContour ReadContour(string path);
}

public enum DxfGeometryQuality
{
    Exact,
    Partial,
    BoundsOnly,
    Invalid
}

public enum DxfGeometryDiagnosticCode
{
    UnsupportedEntity,
    ClosedStateUnavailable,
    BulgeUnavailable,
    InvalidClosedLoop,
    NoClosedContour,
    NoUsableBounds
}

public sealed record DxfGeometryDiagnostic(DxfGeometryDiagnosticCode Code);

public sealed class DxfContour
{
    public Rect Bounds { get; set; }
    public Polygon2D? Polygon { get; set; }
    public List<Polygon2D> Holes { get; set; } = new();
    public DxfGeometryQuality Quality { get; set; } = DxfGeometryQuality.Invalid;
    public List<DxfGeometryDiagnostic> Diagnostics { get; set; } = new();
    public bool HasUsableBounds =>
        double.IsFinite(Bounds.X) && double.IsFinite(Bounds.Y) &&
        double.IsFinite(Bounds.Width) && double.IsFinite(Bounds.Height) &&
        Bounds.Width > 0 && Bounds.Height > 0;
    public bool HasExactGeometry => Quality == DxfGeometryQuality.Exact;

    public double TrueArea =>
        (Polygon?.Area ?? 0.0) - Holes.Sum(h => h.Area);

    public double TruePerimeter =>
        (Polygon?.Perimeter ?? 0.0) + Holes.Sum(h => h.Perimeter);
}
