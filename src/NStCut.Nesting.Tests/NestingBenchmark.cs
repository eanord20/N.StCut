using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NStCut.Core.Contracts;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;
using NStCut.Dxf.Services;
using NStCut.Nesting.Services;
using Xunit;
using Xunit.Abstractions;

namespace NStCut.Nesting.Tests;

public class NestingBenchmark
{
    private readonly ITestOutputHelper _output;

    public NestingBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Part CreatePart(string number, double w, double h, SheetMaterial material,
        GrainMode grain = GrainMode.Allow90, int quantity = 1, Polygon2D? contour = null) => new()
    {
        Number = number,
        Bounds = new Rect(0, 0, w, h),
        Area = contour?.Area ?? w * h,
        MaterialId = material.Id,
        GrainMode = grain,
        Quantity = quantity,
        Contour = contour
    };

    private static Polygon2D LShapeContour(double w, double h, double cutW, double cutH)
    {
        // Counter-clockwise L-shape: outer rectangle (w x h) with a rectangular notch
        // removed from the top-right corner.
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
        // C-shape: outer rectangle with a notch from the right side.
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

    private static SheetMaterial Material()
        => new() { Id = "benchmark-material", Name = "Steel", Length = 2800, Width = 2070 };

    [Fact]
    public void RunAllScenarios_AndWriteAuditReport()
    {
        var material = Material();
        var cases = new[]
        {
            new BenchmarkCase("A", "Identical parts", material,
                new[] { CreatePart("A", 400, 300, material, GrainMode.Allow90, 40) }),

            new BenchmarkCase("B", "Various sizes", material,
                new[]
                {
                    CreatePart("B1", 1200, 600, material, GrainMode.Allow90, 2),
                    CreatePart("B2", 1000, 500, material, GrainMode.Allow90, 2),
                    CreatePart("B3", 800, 700, material, GrainMode.Allow90, 1),
                    CreatePart("B4", 600, 400, material, GrainMode.Allow90, 3),
                    CreatePart("B5", 500, 300, material, GrainMode.Allow90, 4),
                    CreatePart("B6", 400, 300, material, GrainMode.Allow90, 5),
                    CreatePart("B7", 300, 200, material, GrainMode.Allow90, 6)
                }),

            new BenchmarkCase("C", "Large + many small", material,
                new[]
                {
                    CreatePart("C1", 1800, 1000, material, GrainMode.Allow90, 1),
                    CreatePart("C2", 200, 200, material, GrainMode.Allow90, 30)
                }),

            new BenchmarkCase("D", "Parts that benefit from rotation", material,
                new[]
                {
                    CreatePart("D1", 900, 400, material, GrainMode.Allow90, 5),
                    CreatePart("D2", 700, 300, material, GrainMode.Allow90, 5),
                    CreatePart("D3", 1200, 500, material, GrainMode.Allow90, 2),
                    CreatePart("D4", 500, 900, material, GrainMode.Allow90, 3)
                }),

            new BenchmarkCase("E", "Almost full sheet", material,
                new[]
                {
                    CreatePart("E1", 1800, 1000, material, GrainMode.Allow90, 1),
                    CreatePart("E2", 900, 800, material, GrainMode.Allow90, 1),
                    CreatePart("E3", 400, 300, material, GrainMode.Allow90, 4)
                }),

            new BenchmarkCase("F", "Many parts needing multiple sheets", material,
                new[] { CreatePart("F", 500, 400, material, GrainMode.Allow90, 80) }),

            new BenchmarkCase("G", "Mixed realistic", material,
                new[]
                {
                    CreatePart("G1", 1200, 600, material, GrainMode.Allow90, 2),
                    CreatePart("G2", 1000, 500, material, GrainMode.Allow90, 2),
                    CreatePart("G3", 800, 700, material, GrainMode.Allow90, 1),
                    CreatePart("G4", 600, 400, material, GrainMode.Allow90, 4),
                    CreatePart("G5", 400, 300, material, GrainMode.Allow90, 10),
                    CreatePart("G6", 200, 200, material, GrainMode.Allow90, 20)
                }),

            new BenchmarkCase("H", "L-shaped parts — concave waste", material,
                new[]
                {
                    CreatePart("H1", 1000, 1000, material, GrainMode.Allow90, 2,
                        contour: LShapeContour(1000, 1000, 400, 400)),
                    CreatePart("H2", 300, 300, material, GrainMode.Allow90, 4)
                }),

            new BenchmarkCase("I", "C-shaped part — nested-in-hole opportunity", material,
                new[]
                {
                    CreatePart("I1", 1000, 1000, material, GrainMode.Allow90, 1,
                        contour: CShapeContour(1000, 1000, 300, 300)),
                    CreatePart("I2", 200, 200, material, GrainMode.Allow90, 6)
                }),

            new BenchmarkCase("J", "Greedy trap / Human-better layout", material,
                new[]
                {
                    CreatePart("J1", 1400, 900, material, GrainMode.Allow90, 1),
                    CreatePart("J2", 700, 400, material, GrainMode.Allow90, 3),
                    CreatePart("J3", 400, 200, material, GrainMode.Allow90, 6)
                })
        };

        var options = new NestingOptions
        {
            PartGapMm = 5,
            SheetMarginMm = 5,
            Resolution = 0.01,
            MaxSearchPasses = 40,
            NoImprovementStopCount = 40,
            AcceptableMaxUnplaced = -1,
            AcceptableMaxSheets = -1
        };

        var results = cases
            .Select(c =>
            {
                var sw = Stopwatch.StartNew();
                var engine = new MultiPassNestingEngine(new BoundingBoxShelfEngine());
                var result = engine.Nest(c.Parts, new[] { c.Material }, options);
                sw.Stop();
                return new BenchmarkResult(c, result, engine.LastPassLog, sw.Elapsed);
            })
            .ToList();

        foreach (var r in results)
        {
            _output.WriteLine($"{r.Case.Name}: {r.Elapsed.TotalMilliseconds:F1} ms, sheets={r.Result.Sheets.Count}, unplaced={r.Result.Unplaced.Count}, utilization={r.Result.TotalUtilizationPercent:F2}%, passes={r.PassLog.Count}");
        }

        var report = BuildReport(results);
        var repoRoot = FindRepoRoot();
        var reportsDir = repoRoot is not null
            ? Path.Combine(repoRoot, "docs", "superpowers", "reports")
            : Path.Combine(AppContext.BaseDirectory, "BenchmarkReports");

        Directory.CreateDirectory(reportsDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var timestampedPath = Path.Combine(reportsDir, $"nesting-audit-{timestamp}.md");
        var reportPath = Path.Combine(reportsDir, "nesting-audit-report.md");

        File.WriteAllText(timestampedPath, report, Encoding.UTF8);
        File.WriteAllText(reportPath, report, Encoding.UTF8);

        _output.WriteLine($"Audit report written to {reportPath}");

        Assert.True(File.Exists(reportPath), "Report file should have been written.");
    }

    private static string BuildReport(List<BenchmarkResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Nesting Algorithm Audit");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Engine: {nameof(MultiPassNestingEngine)} -> {nameof(BoundingBoxShelfEngine)}");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Case | Description | Parts | Part Area | Sheets | Unplaced | Util % | Waste Area | Used Area | Residual Area | Pockets | Residual Q | Fillability | Geom | Cut Length | Visual | Passes | Time (ms) | Unique Layouts | Best Pass | Best Updates |");
        sb.AppendLine("|------|-------------|-------|-----------|--------|----------|--------|------------|-----------|---------------|--------|------------|------------|------|------------|--------|--------|----------|----------------|-----------|--------------|");

        foreach (var r in results)
        {
            var c = r.Case;
            var res = r.Result;
            var partCount = c.Parts.Sum(p => p.Quantity);
            var partArea = c.Parts.Sum(p => p.Area * p.Quantity);
            var waste = res.Sheets.Sum(s => s.WasteArea);
            var usedArea = res.Sheets.Sum(s => s.UsedWidth * s.UsedHeight);
            var residual = res.Sheets.Sum(s => s.Pockets.Count > 0 ? s.Pockets.Sum(p => p.Area) : s.FreeRectangles.Sum(r => r.Area));
            var pockets = res.Sheets.Sum(s => s.Pockets.Count);
            var unique = r.PassLog.Select(p => p.LayoutHash).Distinct().Count();
            var (bestPass, bestUpdates) = AnalyzeBest(r.PassLog);
            var score = NestingQualityMetrics.Compute(res);

            sb.AppendLine(
                $"| {c.Name} | {c.Description} | {partCount} | {partArea.ToString("F0", CultureInfo.InvariantCulture)} | {res.Sheets.Count} | {res.Unplaced.Count} | " +
                $"{res.TotalUtilizationPercent.ToString("F2", CultureInfo.InvariantCulture)} | {waste.ToString("F2", CultureInfo.InvariantCulture)} | " +
                $"{usedArea.ToString("F2", CultureInfo.InvariantCulture)} | {residual.ToString("F2", CultureInfo.InvariantCulture)} | {pockets} | {score.ResidualQualityScore.ToString("F4", CultureInfo.InvariantCulture)} | {score.UnplacedFillability} | " +
                $"{score.GeometricCompatibilityScore.ToString("F4", CultureInfo.InvariantCulture)} | {score.TotalCutLength.ToString("F0", CultureInfo.InvariantCulture)} | {score.VisualQualityScore.ToString("F4", CultureInfo.InvariantCulture)} | " +
                $"{r.PassLog.Count} | {r.Elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture)} | {unique} | {bestPass} | {bestUpdates} |");
        }

        sb.AppendLine();

        foreach (var r in results)
        {
            var c = r.Case;
            var partCount = c.Parts.Sum(p => p.Quantity);
            var partArea = c.Parts.Sum(p => p.Area * p.Quantity);
            var usedArea = r.Result.Sheets.Sum(s => s.UsedWidth * s.UsedHeight);
            var score = NestingQualityMetrics.Compute(r.Result);

            sb.AppendLine($"### Case {c.Name} — {c.Description}");
            sb.AppendLine();
            sb.AppendLine($"- Material: {c.Material.Length:F0}x{c.Material.Width:F0} mm");
            sb.AppendLine($"- Total parts: {partCount}");
            sb.AppendLine($"- Total part area: {partArea:F0} mm²");
            sb.AppendLine($"- Best sheets: {r.Result.Sheets.Count}");
            sb.AppendLine($"- Unplaced: {r.Result.Unplaced.Count}");
            sb.AppendLine($"- Utilization: {r.Result.TotalUtilizationPercent:F2}%");
            sb.AppendLine($"- Used bounding area: {usedArea:F2} mm²");
            sb.AppendLine($"- Residual quality score: {score.ResidualQualityScore.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"- Unplaced fillability: {score.UnplacedFillability}");
            sb.AppendLine($"- Geometric compatibility: {score.GeometricCompatibilityScore.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"- Total cut length: {score.TotalCutLength.ToString("F0", CultureInfo.InvariantCulture)} mm");
            sb.AppendLine($"- Visual quality score: {score.VisualQualityScore.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"- Passes: {r.PassLog.Count}");
            sb.AppendLine($"- Unique layout hashes: {r.PassLog.Select(p => p.LayoutHash).Distinct().Count()}");
            sb.AppendLine();

            sb.AppendLine("| Pass | Pack | Heuristic | Orientation | Sheets | Unplaced | Util % | Used Area | Residual Q | Fillability | Geom | Cut Length | Visual | Hash |");
            sb.AppendLine("|------|------|-----------|-------------|--------|----------|--------|-----------|------------|-------------|------|------------|--------|------|");

            foreach (var p in r.PassLog)
            {
                var hash = p.LayoutHash.Length > 8 ? p.LayoutHash[..8] : p.LayoutHash;
                sb.AppendLine(
                    $"| {p.Pass} | {p.PackStrategyIndex} | {p.PlacementHeuristic} | {p.OrientationPreference} | " +
                    $"{p.Sheets} | {p.Unplaced} | {p.UtilizationPercent.ToString("F2", CultureInfo.InvariantCulture)} | " +
                    $"{p.UsedBoundingArea.ToString("F2", CultureInfo.InvariantCulture)} | {p.ResidualQualityScore.ToString("F4", CultureInfo.InvariantCulture)} | " +
                    $"{p.UnplacedFillability} | {p.GeometricCompatibilityScore.ToString("F4", CultureInfo.InvariantCulture)} | {p.TotalCutLength.ToString("F0", CultureInfo.InvariantCulture)} | " +
                    $"{p.VisualQualityScore.ToString("F4", CultureInfo.InvariantCulture)} | {hash} |");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Observations");
        sb.AppendLine();
        sb.AppendLine("- Metrics collected across a full pass cycle (40 passes = all PackStrategy/Heuristic/Orientation combinations once).");
        sb.AppendLine("- Use the per-pass tables to identify which pack strategy, heuristic, and orientation produce the most compact layouts for each scenario.");
        sb.AppendLine("- Duplicate layout hashes indicate deterministic permutations that converged to the same packing, leaving room for smarter pass scheduling.");

        return sb.ToString();
    }

    private static (int BestPass, int BestUpdates) AnalyzeBest(IReadOnlyList<NestingPassResult> log)
    {
        if (log.Count == 0)
            return (0, 0);

        NestingPassResult? best = null;
        var updates = 0;
        var bestPass = 0;

        for (var i = 0; i < log.Count; i++)
        {
            if (best is null || IsPassBetter(log[i], best))
            {
                best = log[i];
                bestPass = log[i].Pass;
                updates++;
            }
        }

        return (bestPass, updates);
    }

    private static bool IsPassBetter(NestingPassResult candidate, NestingPassResult current)
    {
        var candidateScore = new NestingQualityScore(
            candidate.Unplaced,
            candidate.Sheets,
            candidate.UtilizationPercent,
            candidate.UsedBoundingArea,
            0.0,
            candidate.ResidualQualityScore,
            candidate.UnplacedFillability,
            candidate.GeometricCompatibilityScore,
            candidate.TotalCutLength,
            candidate.VisualQualityScore,
            candidate.LayoutHash);

        var currentScore = new NestingQualityScore(
            current.Unplaced,
            current.Sheets,
            current.UtilizationPercent,
            current.UsedBoundingArea,
            0.0,
            current.ResidualQualityScore,
            current.UnplacedFillability,
            current.GeometricCompatibilityScore,
            current.TotalCutLength,
            current.VisualQualityScore,
            current.LayoutHash);

        return candidateScore.IsBetterThan(currentScore);
    }

    private static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent is null || parent.FullName == dir)
                break;
            dir = parent.FullName;
        }

        return null;
    }

    private sealed record BenchmarkCase(string Name, string Description, SheetMaterial Material, IReadOnlyList<Part> Parts);

    private sealed record BenchmarkResult(BenchmarkCase Case, NestingResult Result, IReadOnlyList<NestingPassResult> PassLog, TimeSpan Elapsed);
}
