using System.IO;
using System.Linq;
using NStCut.Core.Constants;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Nesting.Services;
using Xunit.Abstractions;

namespace NStCut.Nesting.Tests;

public class MultiPassNestingEngineTests
{
    private readonly ITestOutputHelper _output;

    public MultiPassNestingEngineTests(ITestOutputHelper output)
    {
        _output = output;
    }

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

    [Fact]
    public void Nest_StopsOnAcceptableMaxSheets()
    {
        var material = new SheetMaterial { Length = 45, Width = 32 };
        var parts = new List<Part>
        {
            CreatePart("A", 16, 8, material.Id)
        };
        parts[0].Quantity = 2;

        var engine = new MultiPassNestingEngine(new BoundingBoxShelfEngine());
        var options = new NestingOptions
        {
            PartGapMm = 0,
            SheetMarginMm = 0,
            Resolution = 0.01,
            MaxSearchPasses = 100,
            NoImprovementStopCount = 100,
            AcceptableMaxSheets = 1
        };

        engine.Nest(parts, new[] { material }, options);

        Assert.Equal(1, engine.LastSearchPasses);
        Assert.Single(engine.LastPassLog);
    }

    [Fact]
    public void Nest_WritesLogFile()
    {
        var material = new SheetMaterial { Length = 100, Width = 100 };
        var parts = new List<Part>
        {
            CreatePart("A", 10, 10, material.Id)
        };

        var engine = new MultiPassNestingEngine(new BoundingBoxShelfEngine());
        var logPath = Path.GetTempFileName();
        var options = new NestingOptions
        {
            PartGapMm = 0,
            SheetMarginMm = 0,
            Resolution = 0.01,
            MaxSearchPasses = 3,
            NoImprovementStopCount = 100,
            LogFilePath = logPath
        };

        try
        {
            engine.Nest(parts, new[] { material }, options);

            Assert.True(File.Exists(logPath));
            var content = File.ReadAllText(logPath);
            Assert.Contains("Pass 1", content);
            Assert.Contains("Best result:", content);
        }
        finally
        {
            if (File.Exists(logPath))
                File.Delete(logPath);
        }
    }

    [Fact]
    public void Nest_PassLogContainsAllPasses()
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

        Assert.Equal(5, engine.LastPassLog.Count);
        Assert.Equal(1, engine.LastPassLog[0].Pass);
        Assert.Equal(5, engine.LastPassLog[4].Pass);
    }

    [Fact]
    public void Nest_ControlExperiment_ProducesMultipleUniqueLayoutsAndLogsRanks()
    {
        var material = new SheetMaterial { Length = 2800, Width = 2070 };
        var parts = new List<Part>
        {
            CreatePart("A", 1200, 600, material.Id),
            CreatePart("B", 1000, 500, material.Id),
            CreatePart("C", 800, 700, material.Id),
            CreatePart("D", 600, 400, material.Id),
            CreatePart("E", 500, 300, material.Id),
            CreatePart("F", 400, 300, material.Id)
        };

        var engine = new MultiPassNestingEngine(new BoundingBoxShelfEngine());
        var options = new NestingOptions
        {
            PartGapMm = 0,
            SheetMarginMm = 0,
            Resolution = 0.01,
            MaxSearchPasses = 80,
            NoImprovementStopCount = 80
        };

        var result = engine.Nest(parts, new[] { material }, options);

        _output.WriteLine($"Total passes: {engine.LastSearchPasses}");
        _output.WriteLine($"Unique layouts: {engine.LastPassLog.Select(l => l.LayoutHash).Distinct().Count()}");
        _output.WriteLine($"Best result: sheets={result.Sheets.Count}, unplaced={result.Unplaced.Count}, utilization={result.TotalUtilizationPercent:F2}%, hash={result.LayoutHash}");
        _output.WriteLine($"Preview hash: {result.LayoutHash}");

        _output.WriteLine("Rank | Pass | Sheets | Unplaced | Utilization | UsedArea | Hash");
        var ranked = engine.LastPassLog
            .Select(l => l)
            .OrderBy(l => l.Unplaced)
            .ThenBy(l => l.Sheets)
            .ThenByDescending(l => l.UtilizationPercent)
            .ThenBy(l => l.UsedBoundingArea)
            .Take(5)
            .ToList();
        for (var i = 0; i < ranked.Count; i++)
        {
            var p = ranked[i];
            _output.WriteLine($"{i + 1,4} | {p.Pass,4} | {p.Sheets,6} | {p.Unplaced,8} | {p.UtilizationPercent,11:F2}% | {p.UsedBoundingArea,8:F0} | {p.LayoutHash}");
        }

        Assert.True(engine.LastPassLog.Count > 1, "At least two passes should be recorded.");
        Assert.True(engine.LastPassLog.Select(l => l.LayoutHash).Distinct().Count() > 1, "Multiple passes must produce multiple unique layouts.");
        Assert.Contains(result.LayoutHash, engine.LastPassLog.Select(l => l.LayoutHash));
    }
}
