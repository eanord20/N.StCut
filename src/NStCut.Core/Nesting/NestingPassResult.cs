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

    public override string ToString()
        => $"Pass {Pass}: pack={PackStrategyIndex}, heuristic={PlacementHeuristic}, orientation={OrientationPreference}, sheets={Sheets}, unplaced={Unplaced}, utilization={UtilizationPercent:F2}%, duration={DurationMs}ms";
}
