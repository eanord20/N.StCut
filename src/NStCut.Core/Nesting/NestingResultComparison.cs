using NStCut.Core.Models;

namespace NStCut.Core.Nesting;

/// <summary>Comparison logic used to select the best result from multiple nesting attempts.</summary>
public static class NestingResultComparison
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="candidate"/> is strictly better than <paramref name="current"/>.
    /// Better means fewer unplaced parts, then fewer sheets, then higher total utilization.
    /// </summary>
    public static bool IsBetter(NestingResult candidate, NestingResult current)
    {
        if (candidate.Unplaced.Count != current.Unplaced.Count)
            return candidate.Unplaced.Count < current.Unplaced.Count;

        if (candidate.Sheets.Count != current.Sheets.Count)
            return candidate.Sheets.Count < current.Sheets.Count;

        return candidate.TotalUtilizationPercent > current.TotalUtilizationPercent;
    }
}
