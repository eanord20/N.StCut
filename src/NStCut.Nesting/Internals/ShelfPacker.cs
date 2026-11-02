using System.Linq;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;

namespace NStCut.Nesting.Internals;

internal sealed class ShelfPacker
{
    private sealed class Shelf
    {
        public double Y;
        public double Height;
        public double UsedWidth;
    }

    private readonly SheetMaterial _material;
    private readonly double _margin;
    private readonly double _gap;
    private readonly double _resolution;
    private readonly double _usableWidth;
    private readonly double _usableHeight;
    private readonly List<Shelf> _shelves = new();

    public List<PlacedPart> Parts { get; } = new();
    public double PlacedArea { get; private set; }

    public ShelfPacker(SheetMaterial material, double margin, double gap, double resolution)
    {
        _material = material;
        _margin = double.IsFinite(margin) ? Math.Max(0, margin) : 0;
        _gap = double.IsFinite(gap) ? Math.Max(0, gap) : 0;
        _resolution = double.IsFinite(resolution) ? Math.Max(0, resolution) : 0;
        _usableWidth = material.Length - 2 * _margin;
        _usableHeight = material.Width - 2 * _margin;
    }

    public bool TryPlace(Part part, int copyIndex)
    {
        var orientations = OrientationHelper.GetOrientations(part, _material, _resolution).ToList();
        Shelf? bestClosedOrGrowShelf = null;
        double bestNewHeightForGrow = 0;
        bool bestIsNewShelf = false;
        double bestNewShelfY = 0;
        double bestW = 0, bestH = 0, bestEffectiveW = 0, bestEffectiveH = 0;
        double bestWaste = double.MaxValue;
        bool found = false;

        foreach (var (w, h) in orientations)
        {
            var effectiveW = w + _gap;
            var effectiveH = h + _gap;
            if (!NestingComparison.FitsWithin(effectiveW, _usableWidth) ||
                !NestingComparison.FitsWithin(effectiveH, _usableHeight))
                continue;

            // 1) Закрытые ряды (все, кроме последнего) — переиспользуем свободную ширину без
            //    изменения уже зафиксированной высоты ряда.
            for (int i = 0; i < _shelves.Count - 1; i++)
            {
                var shelf = _shelves[i];
                if (!NestingComparison.FitsWithin(shelf.UsedWidth + effectiveW, _usableWidth)) continue;
                if (!NestingComparison.FitsWithin(effectiveH, shelf.Height)) continue;

                var waste = shelf.Height - effectiveH;
                if (waste < bestWaste)
                {
                    bestWaste = waste;
                    bestClosedOrGrowShelf = shelf;
                    bestNewHeightForGrow = shelf.Height;
                    bestIsNewShelf = false;
                    bestW = w; bestH = h; bestEffectiveW = effectiveW; bestEffectiveH = effectiveH;
                    found = true;
                }
            }

            // 2) Последний (открытый) ряд — можно нарастить высоту, но не выше границы листа.
            if (_shelves.Count > 0)
            {
                var last = _shelves[^1];
                if (NestingComparison.FitsWithin(last.UsedWidth + effectiveW, _usableWidth))
                {
                    var newHeight = Math.Max(last.Height, effectiveH);
                    if (NestingComparison.FitsWithin(last.Y + newHeight, _usableHeight))
                    {
                        var waste = newHeight - effectiveH;
                        if (waste < bestWaste)
                        {
                            bestWaste = waste;
                            bestClosedOrGrowShelf = last;
                            bestNewHeightForGrow = newHeight;
                            bestIsNewShelf = false;
                            bestW = w; bestH = h; bestEffectiveW = effectiveW; bestEffectiveH = effectiveH;
                            found = true;
                        }
                    }
                }
            }
        }

        if (!found)
        {
            foreach (var (w, h) in orientations)
            {
                var effectiveW = w + _gap;
                var effectiveH = h + _gap;
                if (!NestingComparison.FitsWithin(effectiveW, _usableWidth) ||
                    !NestingComparison.FitsWithin(effectiveH, _usableHeight))
                    continue;

                var newShelfY = _shelves.Count == 0 ? 0 : _shelves[^1].Y + _shelves[^1].Height;
                if (!NestingComparison.FitsWithin(newShelfY + effectiveH, _usableHeight))
                    continue;

                if (!found || effectiveH < bestEffectiveH ||
                    Math.Abs(effectiveH - bestEffectiveH) < _resolution && effectiveW < bestEffectiveW)
                {
                    bestClosedOrGrowShelf = null;
                    bestIsNewShelf = true;
                    bestNewShelfY = newShelfY;
                    bestW = w;
                    bestH = h;
                    bestEffectiveW = effectiveW;
                    bestEffectiveH = effectiveH;
                    found = true;
                }
            }
        }

        if (!found)
            return false;

        Shelf targetShelf;
        if (bestIsNewShelf)
        {
            targetShelf = new Shelf { Y = bestNewShelfY, Height = bestEffectiveH, UsedWidth = 0 };
            _shelves.Add(targetShelf);
        }
        else
        {
            targetShelf = bestClosedOrGrowShelf!;
            targetShelf.Height = bestNewHeightForGrow;
        }

        var placed = new PlacedPart
        {
            Source = part,
            CopyIndex = copyIndex,
            Position = new Point(_margin + targetShelf.UsedWidth, _margin + targetShelf.Y),
            Rotation = Math.Abs(bestW - part.Bounds.Width) < _resolution ? 0 : 90,
            Bounds = new Rect(_margin + targetShelf.UsedWidth, _margin + targetShelf.Y, bestW, bestH)
        };

        Parts.Add(placed);
        PlacedArea += bestW * bestH;
        targetShelf.UsedWidth += bestEffectiveW;

        return true;
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
        var truePlacedArea = Parts.Sum(p => p.Source.TrueArea);
        sheet.WasteArea = sheetArea - truePlacedArea;
        sheet.UtilizationPercent = sheetArea > 0 ? truePlacedArea / sheetArea * 100.0 : 0.0;
        sheet.UsedWidth = Parts.Count > 0 ? Parts.Max(p => p.Bounds.Right) : 0.0;
        sheet.UsedHeight = Parts.Count > 0 ? Parts.Max(p => p.Bounds.Top) : 0.0;
        return sheet;
    }
}
