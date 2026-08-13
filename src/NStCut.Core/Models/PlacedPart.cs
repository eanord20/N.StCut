using NStCut.Core.Geometry;

namespace NStCut.Core.Models;

public sealed class PlacedPart
{
    public Part Source { get; set; } = null!;
    public int CopyIndex { get; set; }
    public Point Position { get; set; }
    public double Rotation { get; set; }
    public Rect Bounds { get; set; }
}
