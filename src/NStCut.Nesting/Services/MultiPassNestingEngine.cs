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

        NestingResult? best = null;
        var noImprovementCount = 0;
        LastSearchPasses = 0;

        for (var pass = 0; pass < maxPasses && noImprovementCount < noImprovementLimit; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentParts = pass == 0 ? parts : DeterministicPermutationSource.Permute(parts, baseSeed, pass);
            var scaledProgress = progress is null ? null : new ScaledProgress(progress, pass, maxPasses);

            var candidate = _inner.Nest(currentParts, materials, options, scaledProgress, cancellationToken);
            LastSearchPasses++;

            if (best is null || NestingResultComparison.IsBetter(candidate, best))
            {
                best = candidate;
                noImprovementCount = 0;
            }
            else
            {
                noImprovementCount++;
            }
        }

        return best ?? _inner.Nest(parts, materials, options, progress, cancellationToken);
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
