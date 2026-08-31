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
    private readonly PlacementHeuristic _placementHeuristic;
    private readonly OrientationPreference _orientationPreference;
    private readonly List<Rect> _freeRectangles = new();
    private readonly List<Rect> _effectiveBounds = new();

    public List<PlacedPart> Parts { get; } = new();
    public double PlacedArea { get; private set; }
    internal IReadOnlyList<Rect> FreeRectangles => _freeRectangles;
    internal IReadOnlyList<Rect> EffectiveBounds => _effectiveBounds;

    public MaxRectsPacker(SheetMaterial material, double margin, double gap, double resolution,
        PlacementHeuristic placementHeuristic = PlacementHeuristic.ShortSideFit,
        OrientationPreference orientationPreference = OrientationPreference.Original)
    {
        _material = material;
        _margin = margin;
        _gap = gap;
        _resolution = Math.Max(0, resolution);
        _placementHeuristic = placementHeuristic;
        _orientationPreference = orientationPreference;

        var usableWidth = material.Length - 2 * margin;
        var usableHeight = material.Width - 2 * margin;
        if (usableWidth > _resolution && usableHeight > _resolution)
            _freeRectangles.Add(new Rect(0, 0, usableWidth, usableHeight));
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

    public SheetInstance ToSheet(int index)
    {
        var sheet = new SheetInstance { Material = _material, Index = index };
        sheet.Parts.AddRange(Parts);
        var sheetArea = _material.Length * _material.Width;
        sheet.WasteArea = sheetArea - PlacedArea;
        sheet.UtilizationPercent = sheetArea > 0 ? PlacedArea / sheetArea * 100.0 : 0.0;
        return sheet;
    }
}
