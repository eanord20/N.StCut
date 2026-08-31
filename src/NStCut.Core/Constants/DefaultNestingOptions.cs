using NStCut.Core.Models;

namespace NStCut.Core.Constants;

public static class DefaultNestingOptions
{
    public static NestingOptions Value => new()
    {
        PartGapMm = AppConstants.DefaultPartGapMm,
        SheetMarginMm = AppConstants.DefaultSheetMarginMm,
        Resolution = AppConstants.ComparisonResolution,
        MaxSearchPasses = AppConstants.DefaultMaxSearchPasses,
        NoImprovementStopCount = AppConstants.DefaultNoImprovementStopCount,
        AcceptableMaxUnplaced = AppConstants.DefaultAcceptableMaxUnplaced,
        AcceptableMaxSheets = AppConstants.DefaultAcceptableMaxSheets,
        PlacementHeuristic = AppConstants.DefaultPlacementHeuristic,
        OrientationPreference = AppConstants.DefaultOrientationPreference,
        PackStrategyIndex = AppConstants.DefaultPackStrategyIndex
    };
}
