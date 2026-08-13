using NStCut.Core.Geometry;

namespace NStCut.Core.Models;

public sealed class SheetInstance
{
    public SheetMaterial Material { get; set; } = null!;
    public int Index { get; set; }
    public List<PlacedPart> Parts { get; } = new();
    public double UtilizationPercent { get; set; }
    public double WasteArea { get; set; }
    public Rect Bounds => new(0, 0, Material.Length, Material.Width);
}
