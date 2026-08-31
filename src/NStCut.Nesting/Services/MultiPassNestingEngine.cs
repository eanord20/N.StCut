using System.Diagnostics;
using System.Text;
using NStCut.Core.Contracts;
using NStCut.Core.Models;
using NStCut.Core.Nesting;
using NStCut.Nesting.Internals;

namespace NStCut.Nesting.Services;

/// <summary>
/// Wraps an <see cref="INestingEngine"/> and runs multiple deterministic passes.
/// Passes shuffle the part order and keep the best result.
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

        for (var pass = 0; pass < maxPasses && noImprovementCount < noImprovementLimit; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentParts = pass == 0 ? parts : DeterministicPermutationSource.Permute(parts, baseSeed, pass);
            var scaledProgress = progress is null ? null : new ScaledProgress(progress, pass, maxPasses);

            var sw = Stopwatch.StartNew();
            var candidate = _inner.Nest(currentParts, materials, options, scaledProgress, cancellationToken);
            sw.Stop();

            LastSearchPasses++;
            log.Add(new NestingPassResult
            {
                Pass = pass + 1,
                Sheets = candidate.Sheets.Count,
                Unplaced = candidate.Unplaced.Count,
                UtilizationPercent = candidate.TotalUtilizationPercent,
                DurationMs = sw.ElapsedMilliseconds
            });

            if (best is null || NestingResultComparison.IsBetter(candidate, best))
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

        LastPassLog = log;

        if (best is null)
            best = _inner.Nest(parts, materials, options, progress, cancellationToken);

        WriteLogFile(options.LogFilePath, best, log);

        return best;
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
            sb.AppendLine($"Best result: sheets={best.Sheets.Count}, unplaced={best.Unplaced.Count}, utilization={best.TotalUtilizationPercent:F2}%");
            sb.AppendLine();
            sb.AppendLine("Pass log:");
            sb.AppendLine($"{"Pass",-6}{"Sheets",-10}{"Unplaced",-10}{"Utilization",-14}{"Duration ms"}");

            foreach (var entry in log)
            {
                sb.AppendLine($"Pass {entry.Pass,-4}{entry.Sheets,-10}{entry.Unplaced,-10}{entry.UtilizationPercent,-14:F2}{entry.DurationMs}");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
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
