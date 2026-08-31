using NStCut.Core.Constants;
using NStCut.Core.Nesting;

namespace NStCut.Core.Models;

public sealed class NestingOptions
{
    public double PartGapMm { get; set; } = AppConstants.DefaultPartGapMm;
    public double SheetMarginMm { get; set; } = AppConstants.DefaultSheetMarginMm;
    public double Resolution { get; set; } = AppConstants.ComparisonResolution;
    public int MaxSearchPasses { get; set; } = AppConstants.DefaultMaxSearchPasses;
    public int NoImprovementStopCount { get; set; } = AppConstants.DefaultNoImprovementStopCount;
    public int AcceptableMaxUnplaced { get; set; } = AppConstants.DefaultAcceptableMaxUnplaced;
    public int AcceptableMaxSheets { get; set; } = AppConstants.DefaultAcceptableMaxSheets;
    public PlacementHeuristic PlacementHeuristic { get; set; } = AppConstants.DefaultPlacementHeuristic;
    public OrientationPreference OrientationPreference { get; set; } = AppConstants.DefaultOrientationPreference;
    public int PackStrategyIndex { get; set; } = AppConstants.DefaultPackStrategyIndex;
    public string? LogFilePath { get; set; }
}
