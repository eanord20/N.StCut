using System.Linq;
using System.Text.Json.Serialization;

using NStCut.Core.Geometry;

namespace NStCut.Core.Models;

public sealed class Part
{
    /// <summary>Кодовая/числовая часть обозначения детали, например "2835.01.008".</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Текстовая часть названия детали, например "Отбортовка". Может быть пустой.</summary>
    public string Title { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
    public string MaterialId { get; set; } = string.Empty;
    public string DxfPath { get; set; } = string.Empty;
    public PartStatus Status { get; set; } = PartStatus.Pending;
    public bool IsManuallyResolved { get; set; }
    public GrainMode GrainMode { get; set; } = GrainMode.FollowMaterial;

    /// <summary>Участие детали в раскрое/экспорте/отчёте.</summary>
    public bool IsEnabled { get; set; } = true;

    [JsonIgnore]
    public Polygon2D? Contour { get; set; }

    [JsonIgnore]
    public List<Polygon2D> Holes { get; set; } = new();

    public Rect Bounds { get; set; }
    public double Area { get; set; }
    public double Perimeter { get; set; }

    /// <summary>Реальная площадь контура с учётом отверстий; при отсутствии контура — bounding box.</summary>
    public double TrueArea =>
        Area > 0
            ? Area
            : Math.Max(0.0, (Contour?.Area ?? Bounds.Area) - Holes.Sum(h => h.Area));

    /// <summary>Реальный периметр контура с учётом отверстий; при отсутствии контура — bounding box.</summary>
    public double TruePerimeter =>
        Perimeter > 0
            ? Perimeter
            : (Contour?.Perimeter ?? 0.0) + Holes.Sum(h => h.Perimeter)
                + (Contour is null ? 2.0 * (Bounds.Width + Bounds.Height) : 0.0);

    /// <summary>Полное отображаемое имя детали: "Номер Название" (используется для поиска DXF, отчётов и т.д.).</summary>
    [JsonIgnore]
    public string DisplayName => string.Join(" ", new[] { Number, Title }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public Part Clone()
    {
        return new Part
        {
            Number = Number,
            Title = Title,
            Quantity = Quantity,
            MaterialId = MaterialId,
            DxfPath = DxfPath,
            Status = Status,
            IsManuallyResolved = IsManuallyResolved,
            GrainMode = GrainMode,
            IsEnabled = IsEnabled,
            Contour = Contour is null ? null : new Polygon2D(Contour.Vertices),
            Holes = Holes.Select(h => new Polygon2D(h.Vertices)).ToList(),
            Bounds = Bounds,
            Area = Area,
            Perimeter = Perimeter
        };
    }
}
