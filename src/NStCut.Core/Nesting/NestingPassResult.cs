namespace NStCut.Core.Nesting;

/// <summary>
/// Результат одного прохода мульти-проходного движка раскроя.
/// </summary>
public sealed class NestingPassResult
{
    public int Pass { get; init; }
    public int Sheets { get; init; }
    public int Unplaced { get; init; }
    public double UtilizationPercent { get; init; }
    public long DurationMs { get; init; }
    public int PackStrategyIndex { get; init; }
    public PlacementHeuristic PlacementHeuristic { get; init; }
    public OrientationPreference OrientationPreference { get; init; }
    public string LayoutHash { get; init; } = string.Empty;
    public double UsedBoundingArea { get; init; }
    public double ResidualQualityScore { get; init; }
    public int UnplacedFillability { get; init; }
    public double GeometricCompatibilityScore { get; init; }
    public double TotalCutLength { get; init; }
    public double VisualQualityScore { get; init; }

    public override string ToString()
    {
        var hashPrefix = LayoutHash.Length > 0 ? LayoutHash[..System.Math.Min(8, LayoutHash.Length)] : LayoutHash;
        return $"Pass {Pass}: pack={PackStrategyIndex}, heuristic={PlacementHeuristic}, orientation={OrientationPreference}, " +
               $"sheets={Sheets}, unplaced={Unplaced}, utilization={UtilizationPercent:F2}%, " +
               $"usedBoundingArea={UsedBoundingArea:F2}, residualQ={ResidualQualityScore:F4}, fillability={UnplacedFillability}, " +
               $"geom={GeometricCompatibilityScore:F4}, cut={TotalCutLength:F0}, visual={VisualQualityScore:F4}, " +
               $"hash={hashPrefix}, duration={DurationMs}ms";
    }
}
