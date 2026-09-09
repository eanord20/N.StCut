using System.IO;
using NStCut.Core.Contracts;
using NStCut.Core.Models;
using NStCut.Dxf.Services;
using NStCut.Nesting.Services;
using Xunit;
using Xunit.Abstractions;

namespace NStCut.Nesting.Tests;

public class NestingBenchmarkCorpusTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;

    public NestingBenchmarkCorpusTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void RunCorpusScenarios_AndCheckNesting()
    {
        var corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
        Assert.True(Directory.Exists(corpusDir), $"Corpus directory not found: {corpusDir}");

        var material = new SheetMaterial { Id = "benchmark-material", Name = "Steel", Length = 2800, Width = 2070 };
        var geometry = new DxfGeometryService();
        var parts = new List<Part>();

        foreach (var path in Directory.EnumerateFiles(corpusDir, "*.dxf").OrderBy(Path.GetFileName))
        {
            var contour = geometry.ReadContour(path);

            Assert.True(contour.HasUsableBounds, $"Corpus file {Path.GetFileName(path)} should have usable bounds");

            var area = contour.HasExactGeometry ? contour.TrueArea : contour.Bounds.Area;

            parts.Add(new Part
            {
                Number = Path.GetFileNameWithoutExtension(path),
                Bounds = contour.Bounds,
                Area = area,
                MaterialId = material.Id,
                GrainMode = GrainMode.Allow90,
                Quantity = 1,
                Contour = contour.Polygon
            });
        }

        var options = new NestingOptions
        {
            PartGapMm = 5,
            SheetMarginMm = 5,
            Resolution = 0.01,
            MaxSearchPasses = 20,
            NoImprovementStopCount = 20,
            AcceptableMaxUnplaced = -1,
            AcceptableMaxSheets = -1
        };

        var engine = new MultiPassNestingEngine(new BoundingBoxShelfEngine());
        var result = engine.Nest(parts, new[] { material }, options);

        _output.WriteLine($"Corpus: files={parts.Count}, sheets={result.Sheets.Count}, unplaced={result.Unplaced.Count}, utilization={result.TotalUtilizationPercent:F2}%");

        Assert.NotEmpty(result.Sheets);
        Assert.Empty(result.Unplaced);
    }
}
