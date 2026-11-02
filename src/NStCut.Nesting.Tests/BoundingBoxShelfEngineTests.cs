using System.Text.Json;

using NStCut.Core.Constants;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;
using NStCut.Nesting.Internals;
using NStCut.Nesting.Services;

namespace NStCut.Nesting.Tests;

public class BoundingBoxShelfEngineTests
{
    private static Part CreatePart(string name, double w, double h, string materialId, int qty = 1)
    {
        return new Part
        {
            Number = name,
            Quantity = qty,
            MaterialId = materialId,
            Bounds = new Rect(0, 0, w, h),
            Area = w * h,
            GrainMode = GrainMode.Allow90
        };
    }

    [Fact]
    public void Nest_FitsAllOnOneSheet()
    {
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 100 };
        var parts = new List<Part>
        {
            CreatePart("A", 40, 40, material.Id),
            CreatePart("B", 40, 40, material.Id)
        };

        var result = engine.Nest(parts, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);

        Assert.Single(result.Sheets);
        Assert.Equal(2, result.Sheets[0].Parts.Count);
        Assert.Empty(result.Unplaced);
    }

    [Fact]
    public void Nest_CreatesSecondSheetWhenNeeded()
    {
        // Default options add a 5mm sheet margin and 5mm part gap, so the usable
        // area is 50x50 (60 - 2*5). Two 45x45 parts each consume the full usable
        // width/height once gap is added, so the second copy must spill onto a
        // new sheet instance.
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Steel", Length = 60, Width = 60 };
        var parts = new List<Part>
        {
            CreatePart("A", 45, 45, material.Id),
            CreatePart("B", 45, 45, material.Id)
        };

        var result = engine.Nest(parts, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);

        Assert.Equal(2, result.Sheets.Count);
        Assert.Empty(result.Unplaced);
    }

    [Fact]
    public void Nest_MarksOversizedPartAsUnplaced()
    {
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Steel", Length = 10, Width = 10 };
        var parts = new List<Part>
        {
            CreatePart("A", 20, 20, material.Id)
        };

        var result = engine.Nest(parts, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);

        Assert.Empty(result.Sheets);
        Assert.Single(result.Unplaced);
    }

    [Fact]
    public void Nest_MultiStrategySelectorBeatsMaxSideBaseline()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var options = new NestingOptions { SheetMarginMm = 0, PartGapMm = 0, Resolution = 0.01 };
        var sizes = new[] { (4, 6), (3, 8), (6, 6), (7, 2), (8, 2), (7, 7), (8, 1) };
        var parts = sizes.Select((size, index) => CreatePart(index.ToString(), size.Item1, size.Item2, material.Id)).ToArray();
        foreach (var part in parts)
            part.GrainMode = GrainMode.Fixed0;
        var engine = new BoundingBoxShelfEngine();

        var baseline = engine.NestWithStrategy(parts, new[] { material }, options, PackingStrategy.MaxSide);
        var selected = engine.Nest(parts, new[] { material }, options);

        Assert.True(selected.Sheets.Count < baseline.Sheets.Count,
            $"Selected: {selected.Sheets.Count}; MaxSide: {baseline.Sheets.Count}");
        Assert.Equal(parts.Length, selected.Sheets.SelectMany(sheet => sheet.Parts).Count());
        Assert.Empty(selected.Unplaced);
    }

    [Fact]
    public void Nest_SmallerPartReusesExistingOpenShelfInsteadOfCreatingNewShelf()
    {
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Steel", Length = 100, Width = 100 };
        var options = new NestingOptions { SheetMarginMm = 0, PartGapMm = 0, Resolution = 0.01 };
        var parts = new[]
        {
            CreatePart("A", 70, 40, material.Id),
            CreatePart("B", 50, 30, material.Id),
            CreatePart("C", 30, 20, material.Id)
        };
        foreach (var part in parts)
            part.GrainMode = GrainMode.Fixed0;

        var result = engine.Nest(parts, new[] { material }, options);

        var placed = Assert.Single(result.Sheets).Parts;
        Assert.Single(placed, p => p.Source.Number == "C");
        Assert.DoesNotContain(placed, first => placed.Any(second =>
            !ReferenceEquals(first, second) && first.Bounds.Intersects(second.Bounds)));
    }

    [Fact]
    public void ShelfPacker_ReusesClosedShelfWhenItFits()
    {
        var material = new SheetMaterial { Length = 100, Width = 100 };
        var packer = new ShelfPacker(material, 0, 0, 0.01);
        var first = CreatePart("A", 70, 40, material.Id);
        var second = CreatePart("B", 80, 30, material.Id);
        var smaller = CreatePart("C", 30, 35, material.Id);
        first.GrainMode = second.GrainMode = smaller.GrainMode = GrainMode.Fixed0;

        Assert.True(packer.TryPlace(first, 1));
        Assert.True(packer.TryPlace(second, 1));
        Assert.True(packer.TryPlace(smaller, 1));

        Assert.Equal(0, packer.Parts[^1].Bounds.Y);
        Assert.DoesNotContain(packer.Parts, a => packer.Parts.Any(b =>
            !ReferenceEquals(a, b) && a.Bounds.Intersects(b.Bounds)));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(100.009, false)]
    [InlineData(100.011, false)]
    public void ShelfPacker_PhysicalFitNeverExtendsBeyondUsableBoundary(double width, bool expectedFits)
    {
        var material = new SheetMaterial { Length = 100, Width = 50 };
        var packer = new ShelfPacker(material, 0, 0, 0.01);
        var part = CreatePart("Boundary", width, 50, material.Id);
        part.GrainMode = GrainMode.Fixed0;

        Assert.Equal(expectedFits, packer.TryPlace(part, 1));
    }

    [Fact]
    public void Nest_UsesWeightedTotalUtilizationAcrossDifferentMaterials()
    {
        var small = new SheetMaterial { Length = 10, Width = 10 };
        var large = new SheetMaterial { Length = 20, Width = 20 };
        var options = new NestingOptions { SheetMarginMm = 0, PartGapMm = 0, Resolution = 0.01 };
        var parts = new[]
        {
            CreatePart("Small", 10, 10, small.Id),
            CreatePart("Large", 10, 10, large.Id)
        };

        var result = new BoundingBoxShelfEngine().Nest(parts, new[] { small, large }, options);

        Assert.Equal(40, result.TotalUtilizationPercent, 10);
    }

    [Fact]
    public void Nest_ZeroOrNegativeQuantityProducesNoCopies()
    {
        var material = new SheetMaterial { Length = 100, Width = 100 };
        var parts = new[]
        {
            CreatePart("Zero", 10, 10, material.Id, 0),
            CreatePart("Negative", 10, 10, material.Id, -2)
        };

        var result = new BoundingBoxShelfEngine().Nest(parts, new[] { material }, DefaultNestingOptions.Value);

        Assert.Empty(result.Sheets);
        Assert.Empty(result.Unplaced);
    }

    [Fact]
    public void Nest_PreservesCopyIndexesAndUnplacedCopyCounts()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var part = CreatePart("Oversized", 20, 20, material.Id, 3);

        var result = new BoundingBoxShelfEngine().Nest(new[] { part }, new[] { material }, DefaultNestingOptions.Value);

        Assert.Empty(result.Sheets);
        Assert.Equal(3, result.Unplaced.Count);
        Assert.All(result.Unplaced, copy => Assert.Equal(1, copy.Quantity));
        Assert.Equal(3, part.Quantity);
    }

    [Fact]
    public void Nest_IsDeterministicAndPreservesQuantityGrainBoundsGapAndProgress()
    {
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Grained", Length = 80, Width = 60, HasGrain = true };
        var options = new NestingOptions { SheetMarginMm = 3, PartGapMm = 2, Resolution = 0.01 };
        var parts = new[]
        {
            CreatePart("A", 25, 12, material.Id, 3),
            CreatePart("B", 18, 9, material.Id, 2),
            CreatePart("Oversized", 100, 100, material.Id)
        };
        parts[0].GrainMode = GrainMode.Fixed0;
        parts[1].GrainMode = GrainMode.FollowMaterial;
        var progressValues = new List<double>();

        var first = engine.Nest(parts, new[] { material }, options, new InlineProgress(progressValues.Add));
        var second = engine.Nest(parts, new[] { material }, options);

        var placedParts = first.Sheets.SelectMany(s => s.Parts).ToList();
        Assert.Equal(5, placedParts.Count);
        Assert.Equal(new[] { 1, 2, 3 }, placedParts.Where(p => p.Source.Number == "A").Select(p => p.CopyIndex).OrderBy(x => x));
        Assert.Equal(new[] { 1, 2 }, placedParts.Where(p => p.Source.Number == "B").Select(p => p.CopyIndex).OrderBy(x => x));
        Assert.Single(first.Unplaced);
        Assert.All(first.Sheets.SelectMany(s => s.Parts), p => Assert.Equal(0, p.Rotation));
        Assert.All(first.Sheets.SelectMany(s => s.Parts), p =>
        {
            Assert.True(p.Bounds.Left >= options.SheetMarginMm);
            Assert.True(p.Bounds.Bottom >= options.SheetMarginMm);
            Assert.True(p.Bounds.Right <= material.Length - options.SheetMarginMm);
            Assert.True(p.Bounds.Top <= material.Width - options.SheetMarginMm);
        });
        Assert.All(first.Sheets, sheet => Assert.DoesNotContain(sheet.Parts, a => sheet.Parts.Any(b =>
            !ReferenceEquals(a, b) &&
            a.Bounds.Left < b.Bounds.Right + options.PartGapMm &&
            a.Bounds.Right + options.PartGapMm > b.Bounds.Left &&
            a.Bounds.Bottom < b.Bounds.Top + options.PartGapMm &&
            a.Bounds.Top + options.PartGapMm > b.Bounds.Bottom)));
        Assert.Equal(progressValues.OrderBy(x => x), progressValues);
        Assert.Equal(100, progressValues[^1]);
        Assert.Equal(Serialize(first), Serialize(second));
    }

    [Fact]
    public void Nest_PlacedMultiPartRunsHaveDeterministicLayoutHashAndPlacements()
    {
        var material = new SheetMaterial { Length = 10, Width = 10 };
        var options = new NestingOptions { SheetMarginMm = 0, PartGapMm = 0, Resolution = 0.01 };
        var parts = new[]
        {
            CreatePart("A", 6, 10, material.Id),
            CreatePart("B", 6, 10, material.Id),
            CreatePart("C", 4, 4, material.Id)
        };
        var engine = new BoundingBoxShelfEngine();

        var first = engine.Nest(parts, new[] { material }, options);
        var second = engine.Nest(parts, new[] { material }, options);
        var firstPlacements = first.Sheets.SelectMany(sheet => sheet.Parts.Select(part =>
            (sheet.Index, part.Source.Number, part.CopyIndex, part.Position, part.Rotation))).ToArray();
        var secondPlacements = second.Sheets.SelectMany(sheet => sheet.Parts.Select(part =>
            (sheet.Index, part.Source.Number, part.CopyIndex, part.Position, part.Rotation))).ToArray();

        Assert.Equal(3, firstPlacements.Length);
        Assert.NotEmpty(first.LayoutHash);
        Assert.Equal(LayoutHash.Compute(first.Sheets), first.LayoutHash);
        Assert.Equal(first.LayoutHash, second.LayoutHash);
        Assert.Equal(firstPlacements, secondPlacements);
    }

    [Fact]
    public void Nest_RotationReducesSheetCount()
    {
        // Usable area is 35x22 (margin 5 on a 45x32 sheet). Two 16x8 parts placed
        // unrotated fill the row width (2*(16+5)=42 > 35) and the wrapped row also
        // exceeds the usable height (2*(8+5)=26 > 22), so a second copy needs a new
        // sheet when rotation is disallowed. When 90-degree rotation is allowed,
        // the second copy rotates to 8x16 and fits beside the first copy on the
        // same shelf, so everything fits on a single sheet.
        var engine = new BoundingBoxShelfEngine();
        var material = new SheetMaterial { Name = "Steel", Length = 45, Width = 32 };

        var noRotation = new List<Part>
        {
            CreatePart("A", 16, 8, material.Id, qty: 2)
        };
        noRotation[0].GrainMode = GrainMode.Fixed0;

        var withRotation = new List<Part>
        {
            CreatePart("A", 16, 8, material.Id, qty: 2)
        };
        withRotation[0].GrainMode = GrainMode.Allow90;

        var r1 = engine.Nest(noRotation, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);
        var r2 = engine.Nest(withRotation, new List<SheetMaterial> { material }, DefaultNestingOptions.Value);

        Assert.Single(r1.Sheets);
        Assert.Single(r2.Sheets);
    }

    private static string Serialize(NestingResult result) => JsonSerializer.Serialize(result.Sheets.Select(sheet => new
    {
        sheet.Material.Id,
        sheet.Index,
        Parts = sheet.Parts.Select(part => new
        {
            part.Source.Number,
            part.CopyIndex,
            part.Position,
            part.Rotation,
            part.Bounds
        }).ToArray()
    }).ToArray());

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
