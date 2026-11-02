using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;

namespace NStCut.Core.Tests;

public class SheetFitEvaluatorTests
{
    private static Part Part(double width, double height, GrainMode grainMode = GrainMode.Allow90)
        => new()
        {
            Bounds = new Rect(0, 0, width, height),
            GrainMode = grainMode
        };

    private static SheetMaterial Material(double length, double width, bool hasGrain = false)
        => new() { Length = length, Width = width, HasGrain = hasGrain };

    private static NestingOptions Options(double margin, double gap)
        => new() { SheetMarginMm = margin, PartGapMm = gap, Resolution = 0.01 };

    [Fact]
    public void Evaluate_FitsAtZeroOrientation()
    {
        var part = Part(40, 20);
        var material = Material(100, 50);
        var options = Options(5, 5); // usable 90x40, effective part 45x25 -> fits

        var result = SheetFitEvaluator.Evaluate(part, material, options);

        Assert.True(result.WasEvaluated);
        Assert.True(result.Fits);
        Assert.False(result.IsOversized);
    }

    [Fact]
    public void Evaluate_FitsOnlyWhenRotated()
    {
        var part = Part(50, 20, GrainMode.Allow90);
        var material = Material(60, 40);
        var options = Options(5, 5); // usable 50x30; 50x20+5 fits, 20x50+5 does not? Actually rotated 25x55 no.
        // Better scenario: part 35x20, material 30x25 usable 20x15? Let's craft clearer.
        var part2 = Part(40, 15, GrainMode.Allow90);
        var mat2 = Material(35, 30);
        var opt2 = Options(5, 5); // usable 25x20; 0° effective 45x20 > 25 width no; 90° effective 20x45 > 20 height no -> not fit
        // Use part 18x24, material 30x25 -> usable 20x15; 0° 23x29 no; 90° 29x23 no -> no fit
        // Let's craft one where rotation helps: part 18x12, material 20x20, margin 1 gap 0 => usable 18x18; 0° 18x12 fits? yes.
        // Need rotation decisive with gap: part 16x8, material 20x15, margin 2, gap 2 => usable 16x11; 0° effective 18x10 width 18>16 no; 90° effective 10x18 height 18>11 no.
        // Try part 12x18, material 20x18, margin 1 gap 1 => usable 18x16; 0° 13x19 height >16; 90° 19x13 width >18? 19>18 no.
        // part 10x17, material 20x18, margin1 gap1 => usable18x16; 0° 11x18 height18>16 no; 90° 18x11 width18<=18 yes, height11<=16 yes -> fits rotated.
        var p = Part(10, 17, GrainMode.Allow90);
        var m = Material(20, 18);
        var o = Options(1, 1);

        var result = SheetFitEvaluator.Evaluate(p, m, o);

        Assert.True(result.Fits);
        Assert.Contains(result.AllowedOrientations, o => o.Width > o.Height); // rotated orientation present
    }

    [Fact]
    public void Evaluate_GrainBlocksRotationAndPreventsFit()
    {
        var part = Part(10, 30, GrainMode.FollowMaterial);
        var material = Material(20, 25, hasGrain: true); // grained
        var options = Options(1, 1); // usable 18x23; 0° effective 11x31 -> no; 90° would fit (31x11) but grain blocks

        var result = SheetFitEvaluator.Evaluate(part, material, options);

        Assert.True(result.WasEvaluated);
        Assert.False(result.Fits);
        Assert.True(result.IsOversized);
        Assert.True(result.GrainBlocksRotation);
        Assert.Single(result.AllowedOrientations);
    }

    [Fact]
    public void Evaluate_FollowMaterialOnNonGrainedSheet_AllowsRotation()
    {
        var part = Part(10, 30, GrainMode.FollowMaterial);
        var material = Material(20, 25, hasGrain: false); // not grained
        var options = Options(1, 1); // usable 18x23; 0° no; 90° effective 31x11 -> width 31>18 no. Doesn't fit anyway.
        // Change dims so rotation fits: part 10x20, material 25x18 => usable 23x16; 0° 11x21 height>16 no; 90° 21x11 fits
        var p2 = Part(10, 20, GrainMode.FollowMaterial);
        var m2 = Material(25, 18, hasGrain: false);

        var result = SheetFitEvaluator.Evaluate(p2, m2, Options(1, 1));

        Assert.True(result.Fits);
        Assert.False(result.GrainBlocksRotation);
        Assert.Equal(2, result.AllowedOrientations.Count);
    }

    [Fact]
    public void Evaluate_MarginReducesUsableArea()
    {
        var part = Part(51, 51);
        var material = Material(100, 100);
        var options = Options(5, 5); // usable 90x90, physical part 51x51 -> fits

        var fits = SheetFitEvaluator.Evaluate(part, material, options);
        Assert.True(fits.Fits);

        var tightOptions = Options(25, 5); // usable 50x50, physical part 51x51 -> no fit
        var noFit = SheetFitEvaluator.Evaluate(part, material, tightOptions);
        Assert.False(noFit.Fits);
        Assert.True(noFit.IsOversized);
        Assert.Equal(50, noFit.UsableSheetWidth, 6);
        Assert.Equal(50, noFit.UsableSheetHeight, 6);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(0, 100)]
    [InlineData(1, 5)]
    public void Evaluate_ExactUsableSizeFitsRegardlessOfPartGap(double margin, double gap)
    {
        var material = Material(100, 50);
        var part = Part(100 - 2 * margin, 50 - 2 * margin, GrainMode.Fixed0);

        var result = SheetFitEvaluator.Evaluate(part, material, Options(margin, gap));

        Assert.True(result.WasEvaluated);
        Assert.True(result.Fits);
    }

    [Fact]
    public void Evaluate_ResolutionNeverAllowsBoundaryOverflow()
    {
        var options = Options(0, 10);
        options.Resolution = 0.5;

        var result = SheetFitEvaluator.Evaluate(
            Part(100.001, 50, GrainMode.Fixed0),
            Material(100, 50),
            options);

        Assert.False(result.Fits);
        Assert.True(result.IsOversized);
    }

    [Fact]
    public void Evaluate_NoFitWhenExceedsBothDimensions()
    {
        var part = Part(120, 80);
        var material = Material(100, 60);
        var options = Options(0, 0);

        var result = SheetFitEvaluator.Evaluate(part, material, options);

        Assert.True(result.WasEvaluated);
        Assert.False(result.Fits);
        Assert.True(result.IsOversized);
        Assert.Equal(120, result.PartWidth);
        Assert.Equal(80, result.PartHeight);
        Assert.Equal(100, result.UsableSheetWidth);
        Assert.Equal(60, result.UsableSheetHeight);
    }

    [Fact]
    public void Evaluate_MissingBounds_NotEvaluatedAndNotOversized()
    {
        var part = new Part { Bounds = default };
        var material = Material(100, 100);
        var options = Options(5, 5);

        var result = SheetFitEvaluator.Evaluate(part, material, options);

        Assert.False(result.WasEvaluated);
        Assert.False(result.IsOversized);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(100.001, false)]
    [InlineData(100.49, false)]
    public void Evaluate_PhysicalFitNeverExtendsBeyondUsableBoundary(double width, bool expectedFits)
    {
        var options = Options(0, 0);
        options.Resolution = 0.5;

        var result = SheetFitEvaluator.Evaluate(
            Part(width, 50, GrainMode.Fixed0),
            Material(100, 50),
            options);

        Assert.Equal(expectedFits, result.Fits);
        Assert.Equal(!expectedFits, result.IsOversized);
    }

    [Fact]
    public void Evaluate_NullMaterial_NotEvaluated()
    {
        var part = Part(10, 10);

        var result = SheetFitEvaluator.Evaluate(part, null!, Options(0, 0));

        Assert.False(result.WasEvaluated);
        Assert.False(result.IsOversized);
    }
}
