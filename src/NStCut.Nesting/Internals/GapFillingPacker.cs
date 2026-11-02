using NStCut.Core.Geometry;
using NStCut.Core.Models;

namespace NStCut.Nesting.Internals;

/// <summary>
/// Post-pass that fits unplaced parts into existing free pockets and moves parts
/// from later sheets into pockets of earlier sheets to reduce total sheet count.
/// </summary>
internal static class GapFillingPacker
{
    public static void FillAndCompact(
        List<MaxRectsPacker> packers,
        List<Part> unplaced,
        NestingOptions options,
        IReadOnlyDictionary<Part, int>? copyIndexOf = null)
    {
        if (!options.EnableGapFilling)
            return;

        FillUnplaced(packers, unplaced, copyIndexOf);
        Compact(packers);
    }

    private static void FillUnplaced(
        List<MaxRectsPacker> packers,
        List<Part> unplaced,
        IReadOnlyDictionary<Part, int>? copyIndexOf)
    {
        var partsToTry = unplaced.OrderBy(p => p.TrueArea).ToList();
        foreach (var part in partsToTry)
        {
            if (!unplaced.Contains(part))
                continue;

            foreach (var packer in packers)
            {
                var analysis = FreeSpaceAnalyzer.Analyze(packer.FreeRectangles);
                if (TryPlaceInPockets(packer, part, analysis, copyIndexOf))
                {
                    unplaced.Remove(part);
                    break;
                }
            }
        }
    }

    private static bool TryPlaceInPockets(
        MaxRectsPacker packer,
        Part part,
        FreeSpaceAnalysis analysis,
        IReadOnlyDictionary<Part, int>? copyIndexOf)
    {
        var pockets = analysis.Pockets
            .Where(p => p.LargestInscribedRectangle.Area > 0)
            .OrderByDescending(p => p.LargestInscribedRectangle.Area)
            .ToList();

        var copyIndex = copyIndexOf is not null && copyIndexOf.TryGetValue(part, out var ci) ? ci : 1;
        foreach (var pocket in pockets)
        {
            if (packer.TryPlaceAt(pocket.LargestInscribedRectangle, part, copyIndex))
                return true;
        }

        return false;
    }

    private static void Compact(List<MaxRectsPacker> packers)
    {
        const int maxIterations = 3;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var movedAny = false;

            for (var targetIndex = packers.Count - 1; targetIndex >= 1; targetIndex--)
            {
                var current = packers[targetIndex];
                var partsBySize = current.Parts
                    .OrderByDescending(p => p.Source.TrueArea)
                    .ToList();

                var moved = new List<PlacedPart>();
                foreach (var part in partsBySize)
                {
                    for (var earlierIndex = 0; earlierIndex < targetIndex; earlierIndex++)
                    {
                        if (packers[earlierIndex].TryPlace(part.Source, part.CopyIndex))
                        {
                            moved.Add(part);
                            break;
                        }
                    }
                }

                if (moved.Count == 0)
                    continue;

                movedAny = true;
                foreach (var m in moved)
                    current.Parts.Remove(m);

                if (current.Parts.Count == 0)
                {
                    packers.RemoveAt(targetIndex);
                }
                else
                {
                    packers[targetIndex] = current.Repack(current.Parts);
                }
            }

            if (!movedAny)
                break;
        }
    }
}
