using System;
using System.Globalization;
using NStCut.Core.Constants;

namespace NStCut.Core.Nesting;

/// <summary>
/// Immutable score vector used to compare two nesting layouts.
/// The comparison is lexicographic: fields are evaluated in the declared order
/// and the first non-equal field decides which layout is better.
/// </summary>
public sealed record NestingQualityScore(
    int UnplacedCount,
    int SheetCount,
    double TrueUtilizationPercent,
    double UsedBoundingArea,
    double MaxUsedDimensionSum,
    double ResidualQualityScore,
    int UnplacedFillability,
    double GeometricCompatibilityScore,
    double TotalCutLength,
    double VisualQualityScore,
    string LayoutHash)
{
    /// <summary>
    /// Returns <c>true</c> when this score is strictly better than <paramref name="other"/>
    /// according to the lexicographic nesting objective function.
    /// </summary>
    public bool IsBetterThan(NestingQualityScore? other)
    {
        if (other is null)
            return true;

        var eps = AppConstants.ComparisonResolution;

        if (UnplacedCount != other.UnplacedCount)
            return UnplacedCount < other.UnplacedCount;

        if (SheetCount != other.SheetCount)
            return SheetCount < other.SheetCount;

        if (!ApproximatelyEqual(TrueUtilizationPercent, other.TrueUtilizationPercent, eps))
            return TrueUtilizationPercent > other.TrueUtilizationPercent;

        if (!ApproximatelyEqual(UsedBoundingArea, other.UsedBoundingArea, eps))
            return UsedBoundingArea < other.UsedBoundingArea;

        if (!ApproximatelyEqual(MaxUsedDimensionSum, other.MaxUsedDimensionSum, eps))
            return MaxUsedDimensionSum < other.MaxUsedDimensionSum;

        if (!ApproximatelyEqual(ResidualQualityScore, other.ResidualQualityScore, eps))
            return ResidualQualityScore > other.ResidualQualityScore;

        if (UnplacedFillability != other.UnplacedFillability)
            return UnplacedFillability < other.UnplacedFillability;

        if (!ApproximatelyEqual(GeometricCompatibilityScore, other.GeometricCompatibilityScore, eps))
            return GeometricCompatibilityScore > other.GeometricCompatibilityScore;

        if (!ApproximatelyEqual(TotalCutLength, other.TotalCutLength, eps))
            return TotalCutLength < other.TotalCutLength;

        if (!ApproximatelyEqual(VisualQualityScore, other.VisualQualityScore, eps))
            return VisualQualityScore > other.VisualQualityScore;

        return string.Compare(LayoutHash, other.LayoutHash, StringComparison.Ordinal) < 0;
    }

    public override string ToString()
        => $"U={UnplacedCount}, S={SheetCount}, Util={TrueUtilizationPercent.ToString("F2", CultureInfo.InvariantCulture)}%, " +
           $"UBA={UsedBoundingArea.ToString("F0", CultureInfo.InvariantCulture)}, MaxDim={MaxUsedDimensionSum.ToString("F0", CultureInfo.InvariantCulture)}, " +
           $"ResQ={ResidualQualityScore.ToString("F4", CultureInfo.InvariantCulture)}, Fill={UnplacedFillability}, " +
           $"Geom={GeometricCompatibilityScore.ToString("F4", CultureInfo.InvariantCulture)}, Cut={TotalCutLength.ToString("F0", CultureInfo.InvariantCulture)}, " +
           $"Vis={VisualQualityScore.ToString("F4", CultureInfo.InvariantCulture)}, Hash={LayoutHash[..Math.Min(8, LayoutHash.Length)]}";

    private static bool ApproximatelyEqual(double a, double b, double eps)
        => Math.Abs(a - b) <= eps;
}