using NStCut.Core.Constants;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Nesting.Services;

namespace NStCut.Nesting.Tests;

public class MultiPassNestingEngineTests
{
    private static Part CreatePart(string name, double w, double h, string materialId)
    {
        return new Part
        {
            Number = name,
            Bounds = new Rect(0, 0, w, h),
            Area = w * h,
            MaterialId = materialId,
            GrainMode = GrainMode.Allow90
        };
    }

    [Fact]
    public void Nest_OnePass_EquivalentToInnerEngine()
    {
        var material = new SheetMaterial { Length = 45, Width = 32 };
        var parts = new List<Part>
        {
            CreatePart("A", 16, 8, material.Id)
        };
        parts[0].Quantity = 2;

        var inner = new BoundingBoxShelfEngine();
        var multi = new MultiPassNestingEngine(inner);

        var options = DefaultNestingOptions.Value;
        options.MaxSearchPasses = 1;

        var innerResult = inner.Nest(parts, new[] { material }, DefaultNestingOptions.Value);
        var multiResult = multi.Nest(parts, new[] { material }, options);

        Assert.Equal(innerResult.Sheets.Count, multiResult.Sheets.Count);
        Assert.Equal(innerResult.Unplaced.Count, multiResult.Unplaced.Count);
    }

    [Fact]
    public void Nest_DeterministicAndStopsEarly()
    {
        var material = new SheetMaterial { Length = 100, Width = 100 };
        var parts = new List<Part>
        {
            CreatePart("A", 10, 10, material.Id)
        };

        var engine = new MultiPassNestingEngine(new BoundingBoxShelfEngine());
        var options = new NestingOptions
        {
            PartGapMm = 0,
            SheetMarginMm = 0,
            Resolution = 0.01,
            MaxSearchPasses = 100,
            NoImprovementStopCount = 1
        };

        var first = engine.Nest(parts, new[] { material }, options);
        var second = engine.Nest(parts, new[] { material }, options);

        Assert.Equal(2, engine.LastSearchPasses);
        Assert.Equal(first.Sheets.Count, second.Sheets.Count);
        Assert.Equal(first.Unplaced.Count, second.Unplaced.Count);
        Assert.Equal(first.TotalUtilizationPercent, second.TotalUtilizationPercent);
    }

    [Fact]
    public void Nest_RunsConfiguredPassesWhenNoImprovementLimitHigh()
    {
        var material = new SheetMaterial { Length = 100, Width = 100 };
        var parts = new List<Part>
        {
            CreatePart("A", 10, 10, material.Id)
        };

        var engine = new MultiPassNestingEngine(new BoundingBoxShelfEngine());
        var options = new NestingOptions
        {
            PartGapMm = 0,
            SheetMarginMm = 0,
            Resolution = 0.01,
            MaxSearchPasses = 5,
            NoImprovementStopCount = 100
        };

        engine.Nest(parts, new[] { material }, options);

        Assert.Equal(5, engine.LastSearchPasses);
    }

    [Fact]
    public void Nest_RotationReducesSheetCount_StillFindsBestWithMultiplePasses()
    {
        var material = new SheetMaterial { Length = 45, Width = 32 };
        var parts = new List<Part>
        {
            CreatePart("A", 16, 8, material.Id)
        };
        parts[0].Quantity = 2;

        var engine = new MultiPassNestingEngine(new BoundingBoxShelfEngine());

        var result = engine.Nest(parts, new[] { material }, DefaultNestingOptions.Value);

        Assert.Single(result.Sheets);
        Assert.Empty(result.Unplaced);
    }
}
