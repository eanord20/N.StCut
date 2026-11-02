using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;

namespace NStCut.Nesting.Tests;

public class NestingResultComparisonTests
{
    private static SheetMaterial Material(double length = 100, double width = 100)
        => new() { Length = length, Width = width };

    private static Part Part(double w, double h)
        => new()
        {
            Number = "P",
            Bounds = new Rect(0, 0, w, h),
            Area = w * h
        };

    private static PlacedPart Place(Part source, double x, double y, double w, double h)
        => new()
        {
            Source = source,
            Position = new Point(x, y),
            Bounds = new Rect(x, y, w, h)
        };

    [Fact]
    public void IsBetter_FewerUnplacedWins()
    {
        var better = new NestingResult();
        better.Unplaced.Add(new Part());

        var worse = new NestingResult();
        worse.Unplaced.Add(new Part());
        worse.Unplaced.Add(new Part());

        Assert.True(NestingResultComparison.IsBetter(better, worse));
        Assert.False(NestingResultComparison.IsBetter(worse, better));
    }

    [Fact]
    public void IsBetter_SameUnplacedFewerSheetsWins()
    {
        var material = Material();

        var better = new NestingResult();
        better.Sheets.Add(new SheetInstance { Material = material });

        var worse = new NestingResult();
        worse.Sheets.Add(new SheetInstance { Material = material });
        worse.Sheets.Add(new SheetInstance { Material = material });

        Assert.True(NestingResultComparison.IsBetter(better, worse));
        Assert.False(NestingResultComparison.IsBetter(worse, better));
    }

    [Fact]
    public void IsBetter_SameUnplacedAndSheetsHigherUtilizationWins()
    {
        var material = Material();
        var betterPart = Part(40, 40);
        var worsePart = Part(30, 30);

        var better = new NestingResult();
        var betterSheet = new SheetInstance { Material = material };
        betterSheet.Parts.Add(Place(betterPart, 0, 0, 40, 40));
        better.Sheets.Add(betterSheet);

        var worse = new NestingResult();
        var worseSheet = new SheetInstance { Material = material };
        worseSheet.Parts.Add(Place(worsePart, 0, 0, 30, 30));
        worse.Sheets.Add(worseSheet);

        Assert.True(NestingResultComparison.IsBetter(better, worse));
        Assert.False(NestingResultComparison.IsBetter(worse, better));
    }

    [Fact]
    public void IsBetter_IdenticalIsNotBetter()
    {
        var material = Material();

        var a = new NestingResult();
        var sheetA = new SheetInstance { Material = material };
        sheetA.Parts.Add(Place(Part(40, 40), 0, 0, 40, 40));
        a.Sheets.Add(sheetA);

        var b = new NestingResult();
        var sheetB = new SheetInstance { Material = material };
        sheetB.Parts.Add(Place(Part(40, 40), 0, 0, 40, 40));
        b.Sheets.Add(sheetB);

        Assert.False(NestingResultComparison.IsBetter(a, b));
        Assert.False(NestingResultComparison.IsBetter(b, a));
    }

    [Fact]
    public void IsBetter_SameUtilizationBetterCompactnessWins()
    {
        // Two 10x10 parts on a 100x100 sheet => same part area (200) and utilization.
        // Layout A packs them into a 20x10 bounding box; layout B into 10x20.
        // Both have same UsedBoundingArea, so they tie on that metric.
        // Add a difference in MaxUsedDimensionSum to exercise the next criterion.
        var material = Material(100, 100);
        var part = Part(10, 10);

        var a = new NestingResult();
        var sheetA = new SheetInstance { Material = material };
        sheetA.Parts.Add(Place(part, 0, 0, 10, 10));
        sheetA.Parts.Add(Place(part, 10, 0, 10, 10));
        sheetA.UsedWidth = 20;
        sheetA.UsedHeight = 10;
        a.Sheets.Add(sheetA);

        var b = new NestingResult();
        var sheetB = new SheetInstance { Material = material };
        sheetB.Parts.Add(Place(part, 0, 0, 10, 10));
        sheetB.Parts.Add(Place(part, 0, 10, 10, 10));
        sheetB.UsedWidth = 10;
        sheetB.UsedHeight = 20;
        b.Sheets.Add(sheetB);

        // Lower max dimension wins: A has max(20,10)=20, B has max(10,20)=20 -> tie.
        // Add free rectangles to create a residual-quality difference.
        sheetA.FreeRectangles.Add(new Rect(20, 0, 80, 100));
        sheetB.FreeRectangles.Add(new Rect(10, 0, 10, 10));
        sheetB.FreeRectangles.Add(new Rect(0, 20, 10, 80));
        sheetB.FreeRectangles.Add(new Rect(10, 20, 90, 80));

        Assert.True(NestingResultComparison.IsBetter(a, b));
        Assert.False(NestingResultComparison.IsBetter(b, a));
    }

    [Fact]
    public void IsBetter_LayoutHashTieBreakIsDeterministic()
    {
        var material = Material();

        var a = new NestingResult();
        var sheetA = new SheetInstance { Material = material };
        sheetA.Parts.Add(Place(Part(40, 40), 0, 0, 40, 40));
        a.Sheets.Add(sheetA);

        var b = new NestingResult();
        var sheetB = new SheetInstance { Material = material };
        sheetB.Parts.Add(Place(Part(40, 40), 0, 0, 40, 40));
        b.Sheets.Add(sheetB);

        // LayoutHash is computed from the sheets; for identical layouts it must be identical,
        // so neither result is better than the other.
        Assert.False(NestingResultComparison.IsBetter(a, b));
        Assert.False(NestingResultComparison.IsBetter(b, a));
    }
}