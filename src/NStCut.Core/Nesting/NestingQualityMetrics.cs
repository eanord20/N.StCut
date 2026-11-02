using System;
using System.Collections.Generic;
using System.Linq;
using NStCut.Core.Constants;
using NStCut.Core.Geometry;
using NStCut.Core.Models;

namespace NStCut.Core.Nesting;

/// <summary>
/// Computes a multi-level quality score for a nesting result.
/// Uses real contours when available and falls back to bounding-box values otherwise.
/// </summary>
public static class NestingQualityMetrics
{
    public static NestingQualityScore Compute(NestingResult result, NestingOptions? options = null)
    {
        var opts = options ?? new NestingOptions();
        var layoutHash = string.IsNullOrEmpty(result.LayoutHash)
            ? LayoutHash.Compute(result.Sheets)
            : result.LayoutHash;

        var sheets = result.Sheets;
        var totalSheetArea = sheets.Sum(s =>
            ReferenceEquals(s.Material, null) ? 0.0 : s.Material.Length * s.Material.Width);

        double totalPartArea = 0.0;
        double totalUsedBoundingArea = 0.0;
        double totalMaxDim = 0.0;
        double totalCutLength = 0.0;
        double totalContactLength = 0.0;
        double totalFreeArea = 0.0;
        double totalRemnantValue = 0.0;
        var fragmentationCount = 0;
        var longThinWasteCount = 0;
        var deepPocketCount = 0;

        foreach (var sheet in sheets)
        {
            totalPartArea += ComputeSheetPartArea(sheet);
            totalCutLength += ComputeSheetCutLength(sheet);
            totalContactLength += ComputeSheetContactLength(sheet, opts);

            totalUsedBoundingArea += sheet.UsedWidth * sheet.UsedHeight;
            totalMaxDim += Math.Max(sheet.UsedWidth, sheet.UsedHeight);

            var freeMetrics = AnalyzeFreeRectangles(sheet);
            totalFreeArea += freeMetrics.TotalFreeArea;
            totalRemnantValue += freeMetrics.RemnantValue;
            fragmentationCount += freeMetrics.FragmentationCount;
            longThinWasteCount += freeMetrics.LongThinWasteCount;
            deepPocketCount += freeMetrics.DeepPocketCount;
        }

        var utilization = totalSheetArea > 0
            ? totalPartArea / totalSheetArea * 100.0
            : 0.0;

        var residualQualityScore = ComputeResidualQualityScore(
            totalFreeArea,
            totalRemnantValue,
            fragmentationCount,
            longThinWasteCount,
            deepPocketCount,
            totalSheetArea);

        var unplacedFillability = ComputeUnplacedFillability(result.Unplaced, sheets, opts);

        var totalPartPerimeter = sheets
            .SelectMany(s => s.Parts)
            .Sum(p => ComputePartPerimeter(p));

        var geometricCompatibilityScore = totalPartPerimeter > 0
            ? totalContactLength / totalPartPerimeter
            : 0.0;

        var visualQualityScore = ComputeVisualQualityScore(
            totalPartArea,
            totalUsedBoundingArea,
            totalFreeArea,
            totalRemnantValue,
            fragmentationCount,
            longThinWasteCount,
            sheets);

        return new NestingQualityScore(
            result.Unplaced.Count,
            sheets.Count,
            utilization,
            totalUsedBoundingArea,
            totalMaxDim,
            residualQualityScore,
            unplacedFillability,
            geometricCompatibilityScore,
            totalCutLength,
            visualQualityScore,
            layoutHash);
    }

    private static double ComputeSheetPartArea(SheetInstance sheet)
        => sheet.Parts.Sum(p => ComputePartArea(p));

    private static double ComputeSheetCutLength(SheetInstance sheet)
        => sheet.Parts.Sum(p => ComputePartPerimeter(p));

    private static double ComputePartArea(PlacedPart part)
    {
        if (ReferenceEquals(part.Source, null))
            return part.Bounds.Area;
        return part.Source.TrueArea;
    }

    private static double ComputePartPerimeter(PlacedPart part)
    {
        if (ReferenceEquals(part.Source, null))
            return 2.0 * (part.Bounds.Width + part.Bounds.Height);
        return part.Source.TruePerimeter;
    }

    private static double ComputeSheetContactLength(SheetInstance sheet, NestingOptions options)
    {
        var parts = sheet.Parts;
        if (parts.Count < 2)
            return 0.0;

        var tolerance = options.Resolution;
        var gap = options.PartGapMm;
        var contact = 0.0;

        for (var i = 0; i < parts.Count; i++)
        {
            var a = parts[i].Bounds;
            for (var j = i + 1; j < parts.Count; j++)
            {
                var b = parts[j].Bounds;
                contact += ContactLength(a, b, gap, tolerance);
            }
        }

        return contact;
    }

    private static double ContactLength(Rect a, Rect b, double gap, double tolerance)
    {
        var threshold = gap + tolerance;

        var xSeparation = b.Left - a.Right;
        var ySeparation = a.Top - b.Bottom;

        var horizontalContact = Math.Abs(xSeparation) <= threshold && xSeparation >= -tolerance;
        var verticalContact = Math.Abs(ySeparation) <= threshold && ySeparation >= -tolerance;

        double length = 0.0;

        if (horizontalContact)
        {
            var overlap = Math.Min(a.Top, b.Top) - Math.Max(a.Bottom, b.Bottom);
            if (overlap > tolerance)
                length += overlap;
        }

        if (verticalContact)
        {
            var overlap = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
            if (overlap > tolerance)
                length += overlap;
        }

        return length;
    }

    private static FreeMetrics AnalyzeFreeRectangles(SheetInstance sheet)
    {
        var analysis = sheet.Pockets.Count > 0
            ? FromPockets(sheet.Pockets)
            : FreeSpaceAnalyzer.Analyze(sheet.FreeRectangles);

        return new FreeMetrics(
            analysis.UnionArea,
            analysis.WeightedRemnantValue,
            analysis.FragmentationCount,
            analysis.LongThinWasteCount,
            analysis.DeepPocketCount);
    }

    private static FreeSpaceAnalysis FromPockets(IReadOnlyList<Pocket> pockets)
    {
        var unionArea = pockets.Sum(p => p.Area);
        var valuableArea = pockets.Where(p => p.IsValuable).Sum(p => p.Area);
        var usefulArea = pockets.Where(p => p.IsUseful).Sum(p => p.Area);
        var longThinCount = pockets.Count(p => p.IsLongThin);
        var deepPocketCount = pockets.Count(p => p.IsDeepPocket);
        return new FreeSpaceAnalysis(
            unionArea,
            pockets,
            valuableArea,
            usefulArea,
            unionArea - valuableArea - usefulArea,
            longThinCount,
            deepPocketCount);
    }

    private static double ComputeResidualQualityScore(
        double totalFreeArea,
        double totalRemnantValue,
        int fragmentationCount,
        int longThinWasteCount,
        int deepPocketCount,
        double totalSheetArea)
    {
        if (totalSheetArea <= 0)
            return 0.0;

        if (totalFreeArea <= 0)
            return 1.0; // No leftover space -> perfect residual quality.

        var penalties =
            fragmentationCount * AppConstants.ResidualFragmentationPenaltyMm2 +
            longThinWasteCount * AppConstants.ResidualLongThinPenaltyMm2 +
            deepPocketCount * AppConstants.ResidualDeepPocketPenaltyMm2;

        return (totalRemnantValue - penalties) / totalSheetArea;
    }

    private static int ComputeUnplacedFillability(
        IReadOnlyList<Part> unplaced,
        IReadOnlyList<SheetInstance> sheets,
        NestingOptions options)
    {
        var count = 0;
        foreach (var part in unplaced)
        {
            var fits = false;
            foreach (var sheet in sheets)
            {
                if (ReferenceEquals(sheet.Material, null) || sheet.Material.Id != part.MaterialId)
                    continue;

                foreach (var free in sheet.FreeRectangles)
                {
                    foreach (var (w, h) in OrientationPolicy.GetOrientations(part, sheet.Material, options.Resolution))
                    {
                        if (w + options.PartGapMm <= free.Width + 1e-9
                            && h + options.PartGapMm <= free.Height + 1e-9)
                        {
                            fits = true;
                            break;
                        }
                    }

                    if (fits)
                        break;
                }

                if (fits)
                    break;
            }

            if (fits)
                count++;
        }

        return count;
    }

    private static double ComputeVisualQualityScore(
        double totalPartArea,
        double totalUsedBoundingArea,
        double totalFreeArea,
        double totalRemnantValue,
        int fragmentationCount,
        int longThinWasteCount,
        IReadOnlyList<SheetInstance> sheets)
    {
        var compactness = totalUsedBoundingArea > 0
            ? totalPartArea / totalUsedBoundingArea
            : 0.0;

        var remnantRatio = totalFreeArea > 0
            ? totalRemnantValue / totalFreeArea
            : 1.0;

        var totalPlacedParts = sheets.Sum(s => s.Parts.Count);
        var fragmentationRatio = totalPlacedParts > 0
            ? Math.Min(1.0, fragmentationCount / (double)totalPlacedParts)
            : 0.0;

        var longThinRatio = fragmentationCount > 0
            ? longThinWasteCount / (double)fragmentationCount
            : 0.0;

        return compactness * AppConstants.VisualCompactnessWeight
             + remnantRatio * AppConstants.VisualRemnantWeight
             - fragmentationRatio * AppConstants.VisualFragmentationWeight
             - longThinRatio * AppConstants.VisualLongThinWeight;
    }

    private readonly record struct FreeMetrics(
        double TotalFreeArea,
        double RemnantValue,
        int FragmentationCount,
        int LongThinWasteCount,
        int DeepPocketCount);
}