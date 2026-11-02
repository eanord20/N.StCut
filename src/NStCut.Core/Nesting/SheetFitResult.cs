namespace NStCut.Core.Nesting;

/// <summary>
/// Result of checking whether a part fits on a sheet using current margin/gap and orientation policy.
/// </summary>
public sealed record SheetFitResult(
    bool WasEvaluated,
    bool Fits,
    double PartWidth,
    double PartHeight,
    double UsableSheetWidth,
    double UsableSheetHeight,
    bool GrainBlocksRotation,
    IReadOnlyList<(double Width, double Height)> AllowedOrientations)
{
    public bool IsOversized => WasEvaluated && !Fits;

    public static SheetFitResult NotEvaluated()
        => new(false, false, 0, 0, 0, 0, false, Array.Empty<(double, double)>());
}
