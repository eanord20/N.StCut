using System.Diagnostics;
using System.Text;
using NStCut.Core.Contracts;
using NStCut.Core.Models;
using NStCut.Core.Nesting;
using NStCut.Nesting.Internals;
using NStCut.Nesting.Services;

namespace NStCut.Nesting.Services;

/// <summary>
/// Wraps an <see cref="INestingEngine"/> and runs multiple deterministic passes.
/// Each pass varies the part order, placement heuristic and orientation preference to explore different packings.
/// Duplicate pass inputs are cached and skipped, and passes beyond the first full combo cycle use
/// a deterministic "shake" sequence to explore additional packings.
/// </summary>
public sealed class MultiPassNestingEngine : INestingEngine
{
    private readonly INestingEngine _inner;

    public MultiPassNestingEngine(INestingEngine inner)
    {
        _inner = inner;
    }

    /// <summary>
    /// Number of passes executed during the last <see cref="Nest"/> call.
    /// </summary>
    public int LastSearchPasses { get; private set; }

    /// <summary>
    /// Log of every pass from the last <see cref="Nest"/> call.
    /// </summary>
    public IReadOnlyList<NestingPassResult> LastPassLog { get; private set; } = new List<NestingPassResult>();

    private readonly record struct PassKey(int PackStrategyIndex, int PlacementHeuristic, int Orientation, long SortedHash);

    public NestingResult Nest(
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials,
        NestingOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (options.MaxSearchPasses <= 1)
            return _inner.Nest(parts, materials, options, progress, cancellationToken);

        var maxPasses = options.MaxSearchPasses;
        var noImprovementLimit = options.NoImprovementStopCount > 0
            ? options.NoImprovementStopCount
            : maxPasses;

        var baseSeed = StableHash.Compute(parts, materials, options);

        var log = new List<NestingPassResult>(maxPasses);
        NestingResult? best = null;
        var noImprovementCount = 0;
        LastSearchPasses = 0;

        var heuristics = new[]
        {
            PlacementHeuristic.ShortSideFit,
            PlacementHeuristic.LongSideFit,
            PlacementHeuristic.BestAreaFit,
            PlacementHeuristic.BottomLeft
        };
        var orientations = new[]
        {
            OrientationPreference.Original,
            OrientationPreference.Rotated
        };
        var packStrategies = new[] { 0, 1, 2, 3, 4 };

        var canCache = _inner is BoundingBoxShelfEngine;
        var passCache = canCache
            ? new Dictionary<PassKey, (NestingResult Result, NestingPassResult Log)>()
            : null;

        var originalPlacement = options.PlacementHeuristic;
        var originalOrientation = options.OrientationPreference;
        var originalPackStrategy = options.PackStrategyIndex;

        try
        {
            var comboCount = heuristics.Length * orientations.Length * packStrategies.Length;
            for (var pass = 0; pass < maxPasses && noImprovementCount < noImprovementLimit; pass++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (pass < comboCount)
                {
                    var combo = pass % comboCount;
                    options.PackStrategyIndex = packStrategies[combo % packStrategies.Length];
                    options.PlacementHeuristic = heuristics[(combo / packStrategies.Length) % heuristics.Length];
                    options.OrientationPreference = orientations[(combo / (packStrategies.Length * heuristics.Length)) % orientations.Length];
                }
                else
                {
                    var shakeRng = new DeterministicRandom(baseSeed + pass * 1_000_001L);
                    options.PackStrategyIndex = packStrategies[shakeRng.Next(packStrategies.Length)];
                    options.PlacementHeuristic = heuristics[shakeRng.Next(heuristics.Length)];
                    options.OrientationPreference = orientations[shakeRng.Next(orientations.Length)];
                }

                var currentParts = pass == 0 ? parts : DeterministicPermutationSource.Permute(parts, baseSeed, pass);
                var scaledProgress = progress is null ? null : new ScaledProgress(progress, pass, maxPasses);

                NestingResult candidate;
                NestingPassResult passResult;

                if (canCache &&
                    options.PackStrategyIndex >= 0 &&
                    options.PackStrategyIndex < packStrategies.Length)
                {
                    var sortedHash = PackingSorter.ComputeSortedHash(
                        currentParts,
                        (PackingStrategy)options.PackStrategyIndex);
                    var key = new PassKey(
                        options.PackStrategyIndex,
                        (int)options.PlacementHeuristic,
                        (int)options.OrientationPreference,
                        sortedHash);

                    if (passCache!.TryGetValue(key, out var cached))
                    {
                        candidate = cached.Result;
                        passResult = new NestingPassResult
                        {
                            Pass = pass + 1,
                            Sheets = cached.Log.Sheets,
                            Unplaced = cached.Log.Unplaced,
                            UtilizationPercent = cached.Log.UtilizationPercent,
                            DurationMs = 0,
                            PackStrategyIndex = options.PackStrategyIndex,
                            PlacementHeuristic = options.PlacementHeuristic,
                            OrientationPreference = options.OrientationPreference,
                            LayoutHash = cached.Log.LayoutHash,
                            UsedBoundingArea = cached.Log.UsedBoundingArea,
                            ResidualQualityScore = cached.Log.ResidualQualityScore,
                            UnplacedFillability = cached.Log.UnplacedFillability,
                            GeometricCompatibilityScore = cached.Log.GeometricCompatibilityScore,
                            TotalCutLength = cached.Log.TotalCutLength,
                            VisualQualityScore = cached.Log.VisualQualityScore
                        };
                    }
                    else
                    {
                        (candidate, passResult) = RunPass(
                            _inner, currentParts, materials, options, scaledProgress,
                            cancellationToken, pass + 1);
                        passCache[key] = (candidate, passResult);
                    }
                }
                else
                {
                    (candidate, passResult) = RunPass(
                        _inner, currentParts, materials, options, scaledProgress,
                        cancellationToken, pass + 1);
                }

                LastSearchPasses++;
                log.Add(passResult);

                if (best is null || NestingResultComparison.IsBetter(candidate, best, options))
                {
                    best = candidate;
                    noImprovementCount = 0;
                }
                else
                {
                    noImprovementCount++;
                }

                if (IsAcceptable(best, options))
                    break;
            }
        }
        finally
        {
            options.PlacementHeuristic = originalPlacement;
            options.OrientationPreference = originalOrientation;
            options.PackStrategyIndex = originalPackStrategy;
        }

        LastPassLog = log;

        if (best is null)
            best = _inner.Nest(parts, materials, options, progress, cancellationToken);

        WriteLogFile(options.LogFilePath, best, log);

        return best;
    }

    private static (NestingResult Result, NestingPassResult Log) RunPass(
        INestingEngine inner,
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials,
        NestingOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken,
        int passNumber)
    {
        var sw = Stopwatch.StartNew();
        var candidate = inner.Nest(parts, materials, options, progress, cancellationToken);
        sw.Stop();

        var score = NestingQualityMetrics.Compute(candidate, options);
        var log = new NestingPassResult
        {
            Pass = passNumber,
            Sheets = candidate.Sheets.Count,
            Unplaced = candidate.Unplaced.Count,
            UtilizationPercent = candidate.TotalUtilizationPercent,
            DurationMs = sw.ElapsedMilliseconds,
            PackStrategyIndex = options.PackStrategyIndex,
            PlacementHeuristic = options.PlacementHeuristic,
            OrientationPreference = options.OrientationPreference,
            LayoutHash = score.LayoutHash,
            UsedBoundingArea = score.UsedBoundingArea,
            ResidualQualityScore = score.ResidualQualityScore,
            UnplacedFillability = score.UnplacedFillability,
            GeometricCompatibilityScore = score.GeometricCompatibilityScore,
            TotalCutLength = score.TotalCutLength,
            VisualQualityScore = score.VisualQualityScore
        };

        return (candidate, log);
    }

    private static bool IsAcceptable(NestingResult result, NestingOptions options)
    {
        var activeUnplaced = options.AcceptableMaxUnplaced >= 0;
        var activeSheets = options.AcceptableMaxSheets >= 0;
        if (!activeUnplaced && !activeSheets)
            return false;

        var unplacedOk = !activeUnplaced || result.Unplaced.Count <= options.AcceptableMaxUnplaced;
        var sheetsOk = !activeSheets || result.Sheets.Count <= options.AcceptableMaxSheets;
        return unplacedOk && sheetsOk;
    }

    private static void WriteLogFile(string? path, NestingResult best, IReadOnlyList<NestingPassResult> log)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine($"Nesting log — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total passes: {log.Count}");
            sb.AppendLine($"Best result: sheets={best.Sheets.Count}, unplaced={best.Unplaced.Count}, utilization={best.TotalUtilizationPercent:F2}%");
            sb.AppendLine();
            sb.AppendLine("Pass log:");

            foreach (var entry in log)
                sb.AppendLine(entry.ToString());

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Logging must not break the nesting result. Silent fail.
        }
    }

    private sealed class ScaledProgress : IProgress<double>
    {
        private readonly IProgress<double> _inner;
        private readonly int _pass;
        private readonly int _total;

        public ScaledProgress(IProgress<double> inner, int pass, int total)
        {
            _inner = inner;
            _pass = pass;
            _total = total;
        }

        public void Report(double value)
        {
            var scaled = (_pass + value / 100.0) / _total * 100.0;
            _inner.Report(scaled);
        }
    }
}
