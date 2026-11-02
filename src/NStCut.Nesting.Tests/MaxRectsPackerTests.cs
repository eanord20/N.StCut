using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;
using NStCut.Nesting.Internals;
using NStCut.Nesting.Services;

namespace NStCut.Nesting.Tests;

public class MaxRectsPackerTests
{
    private static Part Part(string number, double width, double height, SheetMaterial material,
        GrainMode grain = GrainMode.Fixed0, int quantity = 1) => new()
    {
        Number = number,
        Bounds = new Rect(0, 0, width, height),
        Area = width * height,
        MaterialId = material.Id,
        GrainMode = grain,
        Quantity = quantity
    };

    [Fact]
    public void TryPlace_FillsCavityAboveShorterPartThatShelfCannotUse()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var packer = new MaxRectsPacker(material, 0, 0, 0.001);

        Assert.True(packer.TryPlace(Part("Tall", 6, 10, material), 1));
        Assert.True(packer.TryPlace(Part("Short", 4, 4, material), 1));
        Assert.True(packer.TryPlace(Part("Cavity", 4, 6, material), 1));

        Assert.Equal(new Rect(6, 4, 4, 6), packer.Parts[^1].Bounds);
    }

    [Fact]
    public void SplitAndPrune_AdversarialPlacementsLeaveOnlyValidUnoccupiedFreeRects()
    {
        var material = new SheetMaterial { Length = 12, Width = 10 };
        var packer = new MaxRectsPacker(material, 0, 0, 0.001);

        Assert.True(packer.TryPlace(Part("A", 7, 4, material), 1));
        Assert.True(packer.TryPlace(Part("B", 5, 7, material), 1));
        Assert.True(packer.TryPlace(Part("C", 7, 6, material), 1));

        Assert.All(packer.FreeRectangles, free =>
        {
            Assert.True(free.Width > 0.001 && free.Height > 0.001);
            Assert.DoesNotContain(packer.EffectiveBounds, occupied => free.Intersects(occupied));
        });
        Assert.Equal(packer.FreeRectangles.Count, packer.FreeRectangles.Distinct().Count());
        Assert.DoesNotContain(packer.FreeRectangles, a => packer.FreeRectangles.Any(b =>
            a != b && b.Contains(a)));
    }

    [Fact]
    public void TryPlace_ResolutionPointFiveNeverAllowsOverlapBeyondFreeRectangle()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var packer = new MaxRectsPacker(material, 0, 0, 0.5);

        Assert.True(packer.TryPlace(Part("A", 6, 10, material), 1));
        Assert.False(packer.TryPlace(Part("BeyondFree", 4.4, 10, material), 1));
        AssertLayoutIsPhysicallyValid(packer.Parts, material, 0, 0);
    }

    [Fact]
    public void TryPlace_GapPointNineNineIsNeverReducedByResolutionTolerance()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var packer = new MaxRectsPacker(material, 0, 0.99, 0.5);

        Assert.True(packer.TryPlace(Part("A", 4, 9.01, material), 1));
        Assert.False(packer.TryPlace(Part("WouldReduceGap", 5.02, 9.01, material), 1));
        AssertLayoutIsPhysicallyValid(packer.Parts, material, 0, 0.99);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 25)]
    [InlineData(1, 1)]
    public void TryPlace_ExactUsablePartFitsWithoutEdgeGap(double margin, double gap)
    {
        var material = new SheetMaterial { Length = 100, Width = 50 };
        var packer = new MaxRectsPacker(material, margin, gap, 0.01);
        var part = Part("Exact", 100 - 2 * margin, 50 - 2 * margin, material);

        Assert.True(packer.TryPlace(part, 1));
        var placed = Assert.Single(packer.Parts);
        Assert.Equal(margin, placed.Bounds.Left);
        Assert.Equal(material.Length - margin, placed.Bounds.Right);
        Assert.Equal(margin, placed.Bounds.Bottom);
        Assert.Equal(material.Width - margin, placed.Bounds.Top);
    }

    [Fact]
    public void TryPlace_ResolutionNeverAllowsPhysicalOverflow()
    {
        var material = new SheetMaterial { Length = 100, Width = 50 };
        var packer = new MaxRectsPacker(material, 0, 10, 0.5);

        Assert.False(packer.TryPlace(Part("TooWide", 100.001, 50, material), 1));
    }

    [Fact]
    public void TryPlace_PreservesFullGapOnlyBetweenParts()
    {
        var material = new SheetMaterial { Length = 100, Width = 50 };
        var packer = new MaxRectsPacker(material, 0, 10, 0.5);

        Assert.True(packer.TryPlace(Part("A", 45, 50, material), 1));
        Assert.True(packer.TryPlace(Part("B", 45, 50, material), 1));

        AssertLayoutIsPhysicallyValid(packer.Parts, material, 0, 10);
        Assert.Equal(0, packer.Parts.Min(p => p.Bounds.Left));
        Assert.Equal(100, packer.Parts.Max(p => p.Bounds.Right));
    }

    [Fact]
    public void TryPlaceAt_RejectsTargetOverlappingExistingEffectiveBounds()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var packer = new MaxRectsPacker(material, 1, 1, 0.001);
        Assert.True(packer.TryPlace(Part("A", 4, 4, material), 1));
        Assert.False(packer.TryPlaceAt(new Rect(4, 0, 5, 5), Part("B", 3, 3, material), 1));
    }

    [Fact]
    public void TryPlaceAt_RejectsPhysicalOverflow()
    {
        var material = new SheetMaterial { Length = 100, Width = 50 };
        var packer = new MaxRectsPacker(material, 0, 10, 0.001);

        Assert.False(packer.TryPlaceAt(
            new Rect(0.001, 0, 110, 60), Part("Overflow", 100, 50, material), 1));
    }

    [Fact]
    public void TryPlaceAtAndRepack_PreserveZeroMarginAndPhysicalGapContract()
    {
        var material = new SheetMaterial { Length = 100, Width = 50 };
        var packer = new MaxRectsPacker(material, 0, 10, 0.01);
        Assert.True(packer.TryPlace(Part("A", 45, 50, material), 1));
        Assert.True(packer.TryPlaceAt(new Rect(55, 0, 55, 60), Part("B", 45, 50, material), 1));

        var repacked = packer.Repack(packer.Parts);

        AssertLayoutIsPhysicallyValid(repacked.Parts, material, 0, 10);
    }

    [Theory]
    [InlineData(100, 50, 0, 10, 0.5, true)]
    [InlineData(100.001, 50, 0, 10, 0.5, false)]
    [InlineData(98, 48, 1, 10, 0, true)]
    [InlineData(98.001, 48, 1, 10, 0.5, false)]
    [InlineData(100, 50, 0, 0, 0.5, true)]
    [InlineData(50, 100, 0, 0, 0.5, true)]
    public void EvaluatorAndEmptyPacker_Agree(
        double width, double height, double margin, double gap, double resolution, bool expected)
    {
        var material = new SheetMaterial { Length = 100, Width = 50 };
        var part = Part("Part", width, height, material, GrainMode.Allow90);
        var options = new NestingOptions
        {
            SheetMarginMm = margin,
            PartGapMm = gap,
            Resolution = resolution
        };

        var evaluatorFits = SheetFitEvaluator.Evaluate(part, material, options).Fits;
        var packerFits = new MaxRectsPacker(material, margin, gap, resolution).TryPlace(part, 1);

        Assert.Equal(expected, evaluatorFits);
        Assert.Equal(evaluatorFits, packerFits);
    }

    [Fact]
    public void Repack_RejectsExistingLayoutThatViolatesGap()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var a = Part("A", 4, 4, material);
        var b = Part("B", 4, 4, material);
        var placedA = new PlacedPart
        {
            Source = a,
            CopyIndex = 1,
            Bounds = new Rect(0, 0, 4, 4),
            Position = new Point(0, 0),
            Rotation = 0
        };
        var placedB = new PlacedPart
        {
            Source = b,
            CopyIndex = 1,
            Bounds = new Rect(4, 1, 4, 4),
            Position = new Point(4, 1),
            Rotation = 0
        };
        var packer = new MaxRectsPacker(material, 0, 2, 0.001);
        Assert.Throws<InvalidOperationException>(() => packer.Repack(new[] { placedA, placedB }));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void TryPlace_NonpositiveUsableSheetProducesNoPlacementAndNoInvalidFreeRectangles(double margin)
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var packer = new MaxRectsPacker(material, margin, 0, 0.001);
        Assert.False(packer.TryPlace(Part("A", 1, 1, material), 1));
        Assert.Empty(packer.FreeRectangles);
    }

    [Fact]
    public void Engine_FillsCavityOnOneSheet()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var options = new NestingOptions { SheetMarginMm = 0, PartGapMm = 0, Resolution = 0.001 };
        var parts = new[]
        {
            Part("Tall", 6, 10, material),
            Part("Short", 4, 4, material),
            Part("Cavity", 4, 6, material)
        };

        var result = new BoundingBoxShelfEngine().NestWithStrategy(parts, new[] { material }, options,
            PackingStrategy.MaxSide);

        var sheet = Assert.Single(result.Sheets);
        Assert.Empty(result.Unplaced);
        Assert.Equal(3, sheet.Parts.Count);
        AssertLayoutIsPhysicallyValid(sheet.Parts, material, 0, 0);
    }

    [Fact]
    public void Engine_RespectsUsableBoundsGapQuantityGrainAndExistingSheetOrder()
    {
        var material = new SheetMaterial { Length = 12, Width = 10, HasGrain = true };
        var options = new NestingOptions { SheetMarginMm = 1, PartGapMm = 1, Resolution = 0.001 };
        var parts = new[]
        {
            Part("A", 5, 3, material, GrainMode.Fixed0, 2),
            Part("B", 3, 5, material, GrainMode.Allow90, 2)
        };

        var result = new BoundingBoxShelfEngine().NestWithStrategy(parts, new[] { material }, options,
            PackingStrategy.MaxSide);

        Assert.Equal(4, result.Sheets.Sum(s => s.Parts.Count) + result.Unplaced.Count);
        Assert.All(result.Sheets, sheet =>
            AssertLayoutIsPhysicallyValid(sheet.Parts, material, options.SheetMarginMm, options.PartGapMm));
        Assert.All(result.Sheets.SelectMany(s => s.Parts).Where(p => p.Source.GrainMode == GrainMode.Fixed0),
            p => Assert.Equal(0, p.Rotation));
        Assert.Equal(Enumerable.Range(1, result.Sheets.Count), result.Sheets.Select(s => s.Index));
        if (result.Sheets.Count > 1)
            Assert.True(result.Sheets[0].Parts.Count >= result.Sheets[^1].Parts.Count);
    }

    [Fact]
    public void Engine_TriesExistingSheetsInIndexOrderBeforeCreatingAnother()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var options = new NestingOptions { SheetMarginMm = 0, PartGapMm = 0, Resolution = 0.001 };
        var parts = new[]
        {
            Part("First", 6, 10, material),
            Part("Second", 6, 10, material),
            Part("FitsBoth", 4, 4, material)
        };

        var result = new BoundingBoxShelfEngine().NestWithStrategy(parts, new[] { material }, options,
            PackingStrategy.MaxSide);

        Assert.Equal(2, result.Sheets.Count);
        Assert.Contains(result.Sheets[0].Parts, p => p.Source.Number == "FitsBoth");
        Assert.DoesNotContain(result.Sheets[1].Parts, p => p.Source.Number == "FitsBoth");
    }

    [Fact]
    public void Engine_OversizedCreatesNoEmptySheet_AndRepeatedRunsAreDeterministic()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var options = new NestingOptions { SheetMarginMm = 1, PartGapMm = 1, Resolution = 0.001 };
        var oversized = Part("TooBig", 9, 9, material, GrainMode.Allow90);
        var engine = new BoundingBoxShelfEngine();

        var first = engine.Nest(new[] { oversized }, new[] { material }, options);
        var second = engine.Nest(new[] { oversized }, new[] { material }, options);

        Assert.Empty(first.Sheets);
        Assert.Single(first.Unplaced);
        Assert.Equal(first.Sheets.SelectMany(s => s.Parts).Select(p => (p.Bounds, p.Rotation, p.CopyIndex)),
            second.Sheets.SelectMany(s => s.Parts).Select(p => (p.Bounds, p.Rotation, p.CopyIndex)));
    }

    private static void AssertLayoutIsPhysicallyValid(
        IReadOnlyList<PlacedPart> parts, SheetMaterial material, double margin, double gap)
    {
        Assert.All(parts, placed =>
        {
            Assert.Equal(placed.Bounds.X, placed.Position.X);
            Assert.Equal(placed.Bounds.Y, placed.Position.Y);
            Assert.True(placed.Bounds.Left >= margin);
            Assert.True(placed.Bounds.Bottom >= margin);
            Assert.True(placed.Bounds.Right <= material.Length - margin);
            Assert.True(placed.Bounds.Top <= material.Width - margin);
        });

        for (var first = 0; first < parts.Count; first++)
        for (var second = first + 1; second < parts.Count; second++)
        {
            var a = parts[first].Bounds;
            var b = parts[second].Bounds;
            Assert.True(
                a.Right + gap <= b.Left || b.Right + gap <= a.Left ||
                a.Top + gap <= b.Bottom || b.Top + gap <= a.Bottom,
                $"Parts {first} and {second} overlap or do not preserve gap {gap}.");
        }
    }
}
