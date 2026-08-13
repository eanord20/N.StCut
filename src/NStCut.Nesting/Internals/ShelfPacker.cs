using NStCut.Core.Geometry;
using NStCut.Core.Models;

namespace NStCut.Nesting.Internals;

internal sealed class ShelfPacker
{
    private readonly SheetMaterial _material;
    private readonly double _margin;
    private readonly double _gap;
    private readonly double _usableWidth;
    private readonly double _usableHeight;

    private double _shelfY;
    private double _shelfHeight;
    private double _cursorX;

    public List<PlacedPart> Parts { get; } = new();
    public double PlacedArea { get; private set; }

    public ShelfPacker(SheetMaterial material, double margin, double gap)
    {
        _material = material;
        _margin = margin;
        _gap = gap;
        _usableWidth = material.Length - 2 * margin;
        _usableHeight = material.Width - 2 * margin;
        _shelfY = 0;
        _shelfHeight = 0;
        _cursorX = 0;
    }

    public bool TryPlace(Part part, int copyIndex)
    {
        foreach (var (w, h) in OrientationHelper.GetOrientations(part, _material))
        {
            var effectiveW = w + _gap;
            var effectiveH = h + _gap;

            if (effectiveW > _usableWidth || effectiveH > _usableHeight)
                continue;

            if (_cursorX + effectiveW > _usableWidth)
            {
                if (_shelfY + _shelfHeight + effectiveH > _usableHeight)
                    continue;

                _shelfY += _shelfHeight;
                _shelfHeight = 0;
                _cursorX = 0;
            }

            var placed = new PlacedPart
            {
                Source = part,
                CopyIndex = copyIndex,
                Position = new Point(_margin + _cursorX, _margin + _shelfY),
                Rotation = Math.Abs(w - part.Bounds.Width) < 0.001 ? 0 : 90,
                Bounds = new Rect(_margin + _cursorX, _margin + _shelfY, w, h)
            };

            Parts.Add(placed);
            PlacedArea += w * h;

            _cursorX += effectiveW;
            if (effectiveH > _shelfHeight)
                _shelfHeight = effectiveH;

            return true;
        }

        return false;
    }

    public SheetInstance ToSheet(int index)
    {
        var sheet = new SheetInstance
        {
            Material = _material,
            Index = index,
        };
        sheet.Parts.AddRange(Parts);

        var sheetArea = _material.Length * _material.Width;
        sheet.WasteArea = sheetArea - PlacedArea;
        sheet.UtilizationPercent = sheetArea > 0 ? PlacedArea / sheetArea * 100.0 : 0.0;
        return sheet;
    }
}
