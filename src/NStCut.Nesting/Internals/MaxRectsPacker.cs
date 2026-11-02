using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;

namespace NStCut.Nesting.Internals;

/// <summary>Deterministic MaxRects packer for part bounding boxes.</summary>
internal sealed class MaxRectsPacker
{
    private readonly record struct Candidate(
        Rect FreeRect,
        double Width,
        double Height,
        double EffectiveWidth,
        double EffectiveHeight,
        int OrientationOrder,
        double ShortSideFit,
        double LongSideFit);

    private readonly SheetMaterial _material;
    private readonly double _margin;
    private readonly double _gap;
    private readonly double _resolution;
    private readonly double _usableWidth;
    private readonly double _usableHeight;
    private readonly PlacementHeuristic _placementHeuristic;
    private readonly OrientationPreference _orientationPreference;
    private readonly List<Rect> _freeRectangles = new();
    private readonly List<Rect> _effectiveBounds = new();

    public List<PlacedPart> Parts { get; } = new();
    public double PlacedArea { get; internal set; }
    internal IReadOnlyList<Rect> FreeRectangles => _freeRectangles;
    internal IReadOnlyList<Rect> EffectiveBounds => _effectiveBounds;

    public MaxRectsPacker(SheetMaterial material, double margin, double gap, double resolution,
        PlacementHeuristic placementHeuristic = PlacementHeuristic.ShortSideFit,
        OrientationPreference orientationPreference = OrientationPreference.Original)
    {
        _material = material;
        _margin = double.IsFinite(margin) ? Math.Max(0, margin) : 0;
        _gap = double.IsFinite(gap) ? Math.Max(0, gap) : 0;
        _resolution = double.IsFinite(resolution) ? Math.Max(0, resolution) : 0;
        _placementHeuristic = placementHeuristic;
        _orientationPreference = orientationPreference;

        _usableWidth = material.Length - 2 * _margin;
        _usableHeight = material.Width - 2 * _margin;
        if (_usableWidth > 0 && _usableHeight > 0)
            _freeRectangles.Add(new Rect(0, 0, _usableWidth + _gap, _usableHeight + _gap));
    }

    public bool TryPlace(Part part, int copyIndex)
    {
        Candidate? best = null;
        var orientationOrder = 0;
        foreach (var (width, height) in OrientationHelper.GetOrientations(part, _material, _resolution))
        {
            var effectiveWidth = width + _gap;
            var effectiveHeight = height + _gap;
            foreach (var free in _freeRectangles)
            {
                if (!NestingComparison.FitsWithin(effectiveWidth, free.Width) ||
                    !NestingComparison.FitsWithin(effectiveHeight, free.Height))
                    continue;
                if (!PhysicalBoundsFit(free.X, free.Y, width, height))
                    continue;

                var remainingWidth = Math.Max(0, free.Width - effectiveWidth);
                var remainingHeight = Math.Max(0, free.Height - effectiveHeight);
                var candidate = new Candidate(free, width, height, effectiveWidth, effectiveHeight,
                    orientationOrder, Math.Min(remainingWidth, remainingHeight), Math.Max(remainingWidth, remainingHeight));
                if (best is null || IsBetter(candidate, best.Value))
                    best = candidate;
            }
            orientationOrder++;
        }

        if (best is null)
            return false;

        var selected = best.Value;
        var effectiveBounds = new Rect(selected.FreeRect.X, selected.FreeRect.Y,
            selected.EffectiveWidth, selected.EffectiveHeight);
        if (OverlapsEffectiveBounds(effectiveBounds))
            return false;

        SplitFreeRectangles(effectiveBounds);
        _effectiveBounds.Add(effectiveBounds);

        var x = _margin + selected.FreeRect.X;
        var y = _margin + selected.FreeRect.Y;
        Parts.Add(new PlacedPart
        {
            Source = part,
            CopyIndex = copyIndex,
            Position = new Point(x, y),
            Rotation = selected.OrientationOrder == 0 ? 0 : 90,
            Bounds = new Rect(x, y, selected.Width, selected.Height)
        });
        PlacedArea += selected.Width * selected.Height;
        return true;
    }

    /// <summary>
    /// Places a part inside a specific target free rectangle. Used by the gap-filling
    /// post-pass to fit parts into pockets that are not in the current MaxRects list.
    /// </summary>
    public bool TryPlaceAt(Rect target, Part part, int copyIndex)
    {
        Candidate? best = null;
        var orientationOrder = 0;
        foreach (var (width, height) in OrientationHelper.GetOrientations(part, _material, _resolution))
        {
            var effectiveWidth = width + _gap;
            var effectiveHeight = height + _gap;
            if (!NestingComparison.FitsWithin(effectiveWidth, target.Width) ||
                !NestingComparison.FitsWithin(effectiveHeight, target.Height) ||
                !PhysicalBoundsFit(target.X, target.Y, width, height))
            {
                orientationOrder++;
                continue;
            }

            var remainingWidth = Math.Max(0, target.Width - effectiveWidth);
            var remainingHeight = Math.Max(0, target.Height - effectiveHeight);
            var candidate = new Candidate(target, width, height, effectiveWidth, effectiveHeight,
                orientationOrder, Math.Min(remainingWidth, remainingHeight), Math.Max(remainingWidth, remainingHeight));
            if (best is null || IsBetter(candidate, best.Value))
                best = candidate;

            orientationOrder++;
        }

        if (best is null)
            return false;

        var selected = best.Value;
        var effectiveBounds = new Rect(selected.FreeRect.X, selected.FreeRect.Y,
            selected.EffectiveWidth, selected.EffectiveHeight);
        if (OverlapsEffectiveBounds(effectiveBounds))
            return false;

        SplitFreeRectangles(effectiveBounds);
        _effectiveBounds.Add(effectiveBounds);

        var x = _margin + selected.FreeRect.X;
        var y = _margin + selected.FreeRect.Y;
        Parts.Add(new PlacedPart
        {
            Source = part,
            CopyIndex = copyIndex,
            Position = new Point(x, y),
            Rotation = selected.OrientationOrder == 0 ? 0 : 90,
            Bounds = new Rect(x, y, selected.Width, selected.Height)
        });
        PlacedArea += selected.Width * selected.Height;
        return true;
    }

    private bool PhysicalBoundsFit(double x, double y, double width, double height) =>
        double.IsFinite(x) && double.IsFinite(y) &&
        double.IsFinite(width) && double.IsFinite(height) &&
        x >= 0 && y >= 0 && width > 0 && height > 0 &&
        NestingComparison.FitsWithin(x + width, _usableWidth) &&
        NestingComparison.FitsWithin(y + height, _usableHeight);

    private bool OverlapsEffectiveBounds(Rect occupied) => _effectiveBounds.Any(b => b.Intersects(occupied));

    private bool IsBetter(Candidate candidate, Candidate current)
    {
        return _placementHeuristic switch
        {
            PlacementHeuristic.LongSideFit => CompareLongSideFit(candidate, current, _orientationPreference),
            PlacementHeuristic.BestAreaFit => CompareBestAreaFit(candidate, current, _orientationPreference),
            PlacementHeuristic.BottomLeft => CompareBottomLeft(candidate, current, _orientationPreference),
            _ => CompareShortSideFit(candidate, current, _orientationPreference)
        };
    }

    private static bool CompareShortSideFit(Candidate candidate, Candidate current, OrientationPreference orientation)
    {
        var comparison = candidate.ShortSideFit.CompareTo(current.ShortSideFit);
        if (comparison != 0) return comparison < 0;
        comparison = candidate.LongSideFit.CompareTo(current.LongSideFit);
        if (comparison != 0) return comparison < 0;
        return CompareBottomLeft(candidate, current, orientation);
    }

    private static bool CompareLongSideFit(Candidate candidate, Candidate current, OrientationPreference orientation)
    {
        var comparison = candidate.LongSideFit.CompareTo(current.LongSideFit);
        if (comparison != 0) return comparison < 0;
        comparison = candidate.ShortSideFit.CompareTo(current.ShortSideFit);
        if (comparison != 0) return comparison < 0;
        return CompareBottomLeft(candidate, current, orientation);
    }

    private static bool CompareBestAreaFit(Candidate candidate, Candidate current, OrientationPreference orientation)
    {
        var candidateArea = candidate.FreeRect.Width * candidate.FreeRect.Height;
        var currentArea = current.FreeRect.Width * current.FreeRect.Height;
        var comparison = candidateArea.CompareTo(currentArea);
        if (comparison != 0) return comparison < 0;
        return CompareBottomLeft(candidate, current, orientation);
    }

    private static bool CompareBottomLeft(Candidate candidate, Candidate current, OrientationPreference orientation)
    {
        var comparison = candidate.FreeRect.Y.CompareTo(current.FreeRect.Y);
        if (comparison != 0) return comparison < 0;
        comparison = candidate.FreeRect.X.CompareTo(current.FreeRect.X);
        if (comparison != 0) return comparison < 0;
        return CompareOrientation(candidate, current, orientation);
    }

    private static bool CompareOrientation(Candidate candidate, Candidate current, OrientationPreference orientation)
    {
        if (orientation == OrientationPreference.Rotated)
            return candidate.OrientationOrder > current.OrientationOrder;

        return candidate.OrientationOrder < current.OrientationOrder;
    }

    private void SplitFreeRectangles(Rect occupied)
    {
        var split = new List<Rect>();
        foreach (var free in _freeRectangles)
        {
            if (!free.Intersects(occupied))
            {
                split.Add(free);
                continue;
            }

            AddIfValid(split, new Rect(free.X, free.Y, occupied.Left - free.Left, free.Height));
            AddIfValid(split, new Rect(occupied.Right, free.Y, free.Right - occupied.Right, free.Height));
            AddIfValid(split, new Rect(free.X, free.Y, free.Width, occupied.Bottom - free.Bottom));
            AddIfValid(split, new Rect(free.X, occupied.Top, free.Width, free.Top - occupied.Top));
        }

        var unique = new List<Rect>();
        foreach (var candidate in split
                     .OrderBy(rect => rect.Y)
                     .ThenBy(rect => rect.X)
                     .ThenBy(rect => rect.Width)
                     .ThenBy(rect => rect.Height))
        {
            if (!unique.Any(existing => ApproximatelyEqual(existing, candidate)))
                unique.Add(candidate);
        }

        _freeRectangles.Clear();
        foreach (var candidate in unique)
        {
            if (!unique.Any(other => !ApproximatelyEqual(other, candidate) && ContainsWithTolerance(other, candidate)))
                _freeRectangles.Add(candidate);
        }
    }

    private void AddIfValid(List<Rect> rectangles, Rect rectangle)
    {
        if (rectangle.Width > _resolution && rectangle.Height > _resolution)
            rectangles.Add(rectangle);
    }

    private bool ApproximatelyEqual(Rect first, Rect second) =>
        Math.Abs(first.X - second.X) <= _resolution &&
        Math.Abs(first.Y - second.Y) <= _resolution &&
        Math.Abs(first.Width - second.Width) <= _resolution &&
        Math.Abs(first.Height - second.Height) <= _resolution;

    private bool ContainsWithTolerance(Rect container, Rect contained) =>
        container.Left <= contained.Left + _resolution &&
        container.Bottom <= contained.Bottom + _resolution &&
        container.Right >= contained.Right - _resolution &&
        container.Top >= contained.Top - _resolution;

    /// <summary>Places a part that is already positioned in the sheet, preserving its coordinates.</summary>
    internal void PlaceExisting(PlacedPart existing)
    {
        var localX = existing.Bounds.X - _margin;
        var localY = existing.Bounds.Y - _margin;
        if (!PhysicalBoundsFit(localX, localY, existing.Bounds.Width, existing.Bounds.Height))
            throw new InvalidOperationException("Existing placement lies outside the physical usable sheet bounds.");

        var effectiveW = existing.Bounds.Width + _gap;
        var effectiveH = existing.Bounds.Height + _gap;
        var occupied = new Rect(localX, localY, effectiveW, effectiveH);
        if (OverlapsEffectiveBounds(occupied))
            throw new InvalidOperationException("Existing placement overlaps or violates part gap.");

        SplitFreeRectangles(occupied);
        _effectiveBounds.Add(occupied);
        Parts.Add(existing);
        PlacedArea += existing.Bounds.Width * existing.Bounds.Height;
    }

    /// <summary>
    /// Registers a part placed without sheet margin (soft-margin mode): the part may
    /// extend into the margin band, so the physical-bounds check is skipped, but the
    /// occupied region is still recorded so subsequent placements cannot overlap it.
    /// </summary>
    internal void RegisterRelaxedPlacement(PlacedPart relaxed)
    {
        var localX = relaxed.Bounds.X - _margin;
        var localY = relaxed.Bounds.Y - _margin;
        var effectiveW = relaxed.Bounds.Width + _gap;
        var effectiveH = relaxed.Bounds.Height + _gap;
        var occupied = new Rect(localX, localY, effectiveW, effectiveH);
        if (OverlapsEffectiveBounds(occupied))
            throw new InvalidOperationException("Relaxed placement overlaps or violates part gap.");

        SplitFreeRectangles(occupied);
        _effectiveBounds.Add(occupied);
        Parts.Add(relaxed);
        PlacedArea += relaxed.Bounds.Width * relaxed.Bounds.Height;
    }

    /// <summary>Creates a new packer and re-places the given parts at their existing positions.</summary>
    internal MaxRectsPacker Repack(IEnumerable<PlacedPart> parts)
    {
        var packer = new MaxRectsPacker(_material, _margin, _gap, _resolution, _placementHeuristic, _orientationPreference);
        foreach (var part in parts)
            packer.PlaceExisting(part);
        return packer;
    }

    public SheetInstance ToSheet(int index)
    {
        var sheet = new SheetInstance { Material = _material, Index = index };
        sheet.Parts.AddRange(Parts);
        var sheetArea = _material.Length * _material.Width;
        var truePlacedArea = Parts.Sum(p => p.Source.TrueArea);
        sheet.WasteArea = sheetArea - truePlacedArea;
        sheet.UtilizationPercent = sheetArea > 0 ? truePlacedArea / sheetArea * 100.0 : 0.0;
        sheet.UsedWidth = Parts.Count > 0 ? Parts.Max(p => p.Bounds.Right) : 0.0;
        sheet.UsedHeight = Parts.Count > 0 ? Parts.Max(p => p.Bounds.Top) : 0.0;
        foreach (var free in _freeRectangles)
            sheet.FreeRectangles.Add(new Rect(_margin + free.X, _margin + free.Y, free.Width, free.Height));

        var pocketAnalysis = FreeSpaceAnalyzer.Analyze(_freeRectangles);
        foreach (var pocket in pocketAnalysis.Pockets)
        {
            var offsetCells = pocket.Cells
                .Select(c => new Rect(_margin + c.X, _margin + c.Y, c.Width, c.Height))
                .ToList();
            var largest = pocket.LargestInscribedRectangle;
            var offsetLargest = new Rect(_margin + largest.X, _margin + largest.Y, largest.Width, largest.Height);
            sheet.Pockets.Add(new Pocket(offsetCells, pocket.Area, offsetLargest,
                pocket.IsValuable, pocket.IsUseful, pocket.IsLongThin, pocket.IsDeepPocket));
        }

        return sheet;
    }
}
