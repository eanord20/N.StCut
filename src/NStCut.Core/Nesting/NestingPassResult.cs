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

    public override string ToString()
        => $"Pass {Pass}: sheets={Sheets}, unplaced={Unplaced}, utilization={UtilizationPercent:F2}%, duration={DurationMs}ms";
}
