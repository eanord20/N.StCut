using System.Collections.Generic;
using System.Linq;
using NStCut.Core.Constants;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;
using NStCut.Nesting.Services;
using Xunit;

namespace NStCut.Nesting.Tests;

public class NestingQualityTests
{
    private static SheetMaterial Material(double length = 2800, double width = 2070)
        => new() { Id = "material", Length = length, Width = width };

    private static Part Part(string number, double w, double h, int quantity = 1, Polygon2D? contour = null)
        => new()
        {
            Number = number,
            Bounds = new Rect(0, 0, w, h),
            Area = contour?.Area ?? w * h,
            MaterialId = "material",
            GrainMode = GrainMode.Allow90,
            Quantity = quantity,
            Contour = contour
        };

    private static PlacedPart Place(Part source, double x, double y, double w, double h)
        => new()
        {
            Source = source,
            Position = new Point(x, y),
            Bounds = new Rect(x, y, w, h)
        };

    private static NestingOptions Options(int maxPasses = 40)
        => new()
        {
            PartGapMm = 5,
            SheetMarginMm = 5,
            Resolution = 0.01,
            MaxSearchPasses = maxPasses,
            NoImprovementStopCount = maxPasses,
            AcceptableMaxUnplaced = -1,
            AcceptableMaxSheets = -1
        };

    private static Polygon2D LShapeContour(double w, double h, double cutW, double cutH)
    {
        var points = new List<Point>
        {
            new(0, 0),
            new(w - cutW, 0),
            new(w - cutW, cutH),
            new(w, cutH),
            new(w, h),
            new(0, h)
        };
        return new Polygon2D(points);
    }

    private static Polygon2D CShapeContour(double w, double h, double notchW, double notchH)
    {
        var points = new List<Point>
        {
            new(0, 0),
            new(w, 0),
            new(w, (h - notchH) / 2),
            new(w - notchW, (h - notchH) / 2),
            new(w - notchW, (h + notchH) / 2),
            new(w, (h + notchH) / 2),
            new(w, h),
            new(0, h)
        };
        return new Polygon2D(points);
    }

    private static NestingResult Nest(IReadOnlyList<Part> parts, SheetMaterial material, NestingOptions options)
        => new MultiPassNestingEngine(new BoundingBoxShelfEngine()).Nest(parts, new[] { material }, options);

    [Fact]
    public void Metrics_ComputesUtilizationFromPlacedParts()
    {
        var material = Material(100, 100);
        var part = Part("P", 40, 40);

        var result = new NestingResult();
        var sheet = new SheetInstance { Material = material };
        sheet.Parts.Add(Place(part, 5, 5, 40, 40));
        result.Sheets.Add(sheet);

        var score = NestingQualityMetrics.Compute(result);

        Assert.Equal(1, score.SheetCount);
        Assert.Equal(0, score.UnplacedCount);
        Assert.Equal(16.0, score.TrueUtilizationPercent, 2);
    }

    [Fact]
    public void Metrics_UsesContourAreaWhenAvailable()
    {
        var material = Material(100, 100);
        // L-shape: bounding box 40x40 = 1600, true area 1600 - 20x20 = 1200.
        var part = Part("L", 40, 40, contour: LShapeContour(40, 40, 20, 20));

        var result = new NestingResult();
        var sheet = new SheetInstance { Material = material };
        sheet.Parts.Add(Place(part, 5, 5, 40, 40));
        result.Sheets.Add(sheet);

        var score = NestingQualityMetrics.Compute(result);

        // True area = 1200 / 10000 * 100 = 12%, not 16%.
        Assert.Equal(12.0, score.TrueUtilizationPercent, 2);
    }

    [Fact]
    public void Metrics_PenalizesFragmentedFreeRectangles()
    {
        var material = Material(100, 100);
        var part = Part("P", 40, 40);

        var compact = new NestingResult();
        var sheetA = new SheetInstance { Material = material };
        sheetA.Parts.Add(Place(part, 5, 5, 40, 40));
        sheetA.FreeRectangles.Add(new Rect(5, 45, 90, 50)); // one large free rect
        compact.Sheets.Add(sheetA);

        var fragmented = new NestingResult();
        var sheetB = new SheetInstance { Material = material };
        sheetB.Parts.Add(Place(part, 5, 5, 40, 40));
        sheetB.FreeRectangles.Add(new Rect(5, 45, 40, 50));      // left top
        sheetB.FreeRectangles.Add(new Rect(50, 45, 45, 20));     // right top
        sheetB.FreeRectangles.Add(new Rect(50, 70, 45, 25));     // right upper, separated from the second rect
        fragmented.Sheets.Add(sheetB);

        var scoreA = NestingQualityMetrics.Compute(compact);
        var scoreB = NestingQualityMetrics.Compute(fragmented);

        Assert.True(scoreA.ResidualQualityScore > scoreB.ResidualQualityScore,
            $"Compact {scoreA.ResidualQualityScore} should be higher than fragmented {scoreB.ResidualQualityScore}.");
    }

    [Fact]
    public void Metrics_UnplacedFillability_DetectsMissedPlacement()
    {
        var material = Material(100, 100);
        var placedPart = Part("A", 40, 40);
        var unplacedPart = Part("B", 20, 20);

        var result = new NestingResult();
        var sheet = new SheetInstance { Material = material };
        sheet.Parts.Add(Place(placedPart, 5, 5, 40, 40));
        // A free rectangle large enough for B, but B was not placed.
        sheet.FreeRectangles.Add(new Rect(45, 5, 50, 90));
        result.Sheets.Add(sheet);
        result.Unplaced.Add(unplacedPart);

        var score = NestingQualityMetrics.Compute(result, Options());

        Assert.True(score.UnplacedFillability > 0,
            "Unplaced part that fits in a free rectangle should increase fillability count.");
    }

    [Fact]
    public void Metrics_ContactLength_IncreasesWithAdjacency()
    {
        var material = Material(100, 100);
        var part = Part("P", 20, 20);

        var apart = new NestingResult();
        var sheetA = new SheetInstance { Material = material };
        sheetA.Parts.Add(Place(part, 5, 5, 20, 20));
        sheetA.Parts.Add(Place(part, 50, 5, 20, 20));
        apart.Sheets.Add(sheetA);

        var adjacent = new NestingResult();
        var sheetB = new SheetInstance { Material = material };
        sheetB.Parts.Add(Place(part, 5, 5, 20, 20));
        sheetB.Parts.Add(Place(part, 25, 5, 20, 20)); // gap = 0? plus default gap 5 -> separated by 5
        adjacent.Sheets.Add(sheetB);

        var scoreA = NestingQualityMetrics.Compute(apart, Options());
        var scoreB = NestingQualityMetrics.Compute(adjacent, Options());

        Assert.True(scoreB.GeometricCompatibilityScore > scoreA.GeometricCompatibilityScore,
            "Adjacent parts should have higher geometric compatibility score.");
    }

    [Fact]
    public void Reference_A_IdenticalParts()
    {
        var material = Material();
        var parts = new[] { Part("A", 400, 300, 40) };
        var result = Nest(parts, material, Options());

        Assert.Empty(result.Unplaced);
        Assert.Single(result.Sheets);
        Assert.True(result.TotalUtilizationPercent > 70.0);
    }

    [Fact]
    public void Reference_B_VariousSizes()
    {
        var material = Material();
        var parts = new[]
        {
            Part("B1", 1200, 600, 2),
            Part("B2", 1000, 500, 2),
            Part("B3", 800, 700, 1),
            Part("B4", 600, 400, 3),
            Part("B5", 500, 300, 4),
            Part("B6", 400, 300, 5),
            Part("B7", 300, 200, 6)
        };

        var result = Nest(parts, material, Options());

        Assert.Empty(result.Unplaced);
        Assert.True(result.Sheets.Count <= 2);
    }

    [Fact]
    public void Reference_C_LargeAndManySmall()
    {
        var material = Material();
        var parts = new[]
        {
            Part("C1", 1800, 1000, 1),
            Part("C2", 200, 200, 30)
        };

        var result = Nest(parts, material, Options());

        Assert.Empty(result.Unplaced);
        Assert.True(result.Sheets.Count <= 1);
    }

    [Fact]
    public void Reference_D_Rotation()
    {
        var material = Material();
        var parts = new[]
        {
            Part("D1", 900, 400, 5),
            Part("D2", 700, 300, 5),
            Part("D3", 1200, 500, 2),
            Part("D4", 500, 900, 3)
        };

        var result = Nest(parts, material, Options());

        Assert.Empty(result.Unplaced);
        Assert.True(result.Sheets.Count <= 3);
    }

    [Fact]
    public void Reference_E_AlmostFullSheet()
    {
        var material = Material();
        var parts = new[]
        {
            Part("E1", 1800, 1000, 1),
            Part("E2", 900, 800, 1),
            Part("E3", 400, 300, 4)
        };

        var result = Nest(parts, material, Options());

        Assert.Empty(result.Unplaced);
        Assert.True(result.Sheets.Count <= 1);
    }

    [Fact]
    public void Reference_F_MultipleSheets()
    {
        var material = Material();
        var parts = new[] { Part("F", 500, 400, 80) };

        var result = Nest(parts, material, Options());

        Assert.Empty(result.Unplaced);
        Assert.True(result.Sheets.Count <= 5);
    }

    [Fact]
    public void Reference_G_MixedRealistic()
    {
        var material = Material();
        var parts = new[]
        {
            Part("G1", 1200, 600, 2),
            Part("G2", 1000, 500, 2),
            Part("G3", 800, 700, 1),
            Part("G4", 600, 400, 4),
            Part("G5", 400, 300, 10),
            Part("G6", 200, 200, 20)
        };

        var result = Nest(parts, material, Options());

        Assert.Empty(result.Unplaced);
        Assert.True(result.Sheets.Count <= 3);
    }

    [Fact]
    public void Reference_H_LShape_CurrentEngineUsesBoundingBox()
    {
        var material = Material();
        var parts = new[]
        {
            Part("H1", 1000, 1000, 2, LShapeContour(1000, 1000, 400, 400)),
            Part("H2", 300, 300, 4)
        };

        var result = Nest(parts, material, Options());
        var score = NestingQualityMetrics.Compute(result);

        // Engine still places all parts because bounding boxes fit easily.
        Assert.Empty(result.Unplaced);
        Assert.True(result.Sheets.Count <= 1);

        // Total utilization is now contour-based; it must be lower than the bounding-box-only utilization.
        var sheetArea = result.Sheets[0].Material.Length * result.Sheets[0].Material.Width;
        var bboxArea = result.Sheets[0].Parts.Sum(p => p.Source.Bounds.Area);
        var bboxUtilization = bboxArea / sheetArea * 100.0;
        Assert.True(result.TotalUtilizationPercent < bboxUtilization,
            "True utilization (using contour area) should be lower than bbox-based utilization for L-shaped parts.");
        Assert.Equal(score.TrueUtilizationPercent, result.TotalUtilizationPercent, 2);
    }

    [Fact]
    public void Reference_I_CShape_CurrentEngineDoesNotExploitConcavity()
    {
        var material = Material();
        var parts = new[]
        {
            Part("I1", 1000, 1000, 1, CShapeContour(1000, 1000, 300, 300)),
            Part("I2", 200, 200, 6)
        };

        var result = Nest(parts, material, Options());
        var score = NestingQualityMetrics.Compute(result);

        Assert.Empty(result.Unplaced);
        Assert.True(result.Sheets.Count <= 1);

        var sheetArea = result.Sheets[0].Material.Length * result.Sheets[0].Material.Width;
        var bboxArea = result.Sheets[0].Parts.Sum(p => p.Source.Bounds.Area);
        var bboxUtilization = bboxArea / sheetArea * 100.0;
        Assert.True(result.TotalUtilizationPercent < bboxUtilization,
            "True utilization should be lower than bbox utilization for C-shaped parts.");
        Assert.Equal(score.TrueUtilizationPercent, result.TotalUtilizationPercent, 2);
    }

    [Fact]
    public void Reference_J_GreedyTrap_MultiPassBeatsSingleStrategy()
    {
        var material = Material(2000, 1500);
        var parts = new[]
        {
            Part("J1", 1400, 900, 1),
            Part("J2", 700, 400, 3),
            Part("J3", 400, 200, 6)
        };

        var singlePassOptions = Options(1);
        var multiPassOptions = Options(40);

        var single = Nest(parts, material, singlePassOptions);
        var multi = Nest(parts, material, multiPassOptions);

        var multiScore = NestingQualityMetrics.Compute(multi, multiPassOptions);
        var singleScore = NestingQualityMetrics.Compute(single, singlePassOptions);

        // Multi-pass should be at least as good as a single pass according to the objective function.
        Assert.True(
            !singleScore.IsBetterThan(multiScore),
            "Multi-pass nesting should not be worse than a single pass.");
    }
}