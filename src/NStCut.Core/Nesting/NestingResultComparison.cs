using NStCut.Core.Models;

namespace NStCut.Core.Nesting;

/// <summary>Comparison logic used to select the best result from multiple nesting attempts.</summary>
public static class NestingResultComparison
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="candidate"/> is strictly better than <paramref name="current"/>
    /// according to the full nesting objective function defined in <c>nesting-objective-function.md</c>.
    /// </summary>
    public static bool IsBetter(NestingResult candidate, NestingResult current, NestingOptions? options = null)
    {
        var candidateScore = NestingQualityMetrics.Compute(candidate, options);
        var currentScore = NestingQualityMetrics.Compute(current, options);
        return candidateScore.IsBetterThan(currentScore);
    }
}
