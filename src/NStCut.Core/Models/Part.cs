using System.Text.Json.Serialization;

using NStCut.Core.Geometry;

namespace NStCut.Core.Models;

public sealed class Part
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string MaterialId { get; set; } = string.Empty;
    public string DxfPath { get; set; } = string.Empty;
    public PartStatus Status { get; set; } = PartStatus.Pending;
    public bool IsManuallyResolved { get; set; }
    public GrainMode GrainMode { get; set; } = GrainMode.FollowMaterial;

    [JsonIgnore]
    public Polygon2D? Contour { get; set; }

    public Rect Bounds { get; set; }
    public double Area { get; set; }

    public Part Clone()
    {
        return new Part
        {
            Name = Name,
            Quantity = Quantity,
            MaterialId = MaterialId,
            DxfPath = DxfPath,
            Status = Status,
            IsManuallyResolved = IsManuallyResolved,
            GrainMode = GrainMode,
            Contour = Contour is null ? null : new Polygon2D(Contour.Vertices),
            Bounds = Bounds,
            Area = Area
        };
    }
}
