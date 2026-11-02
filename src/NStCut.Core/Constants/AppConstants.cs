using NStCut.Core.Nesting;

namespace NStCut.Core.Constants;

public static class AppConstants
{
    public const double DefaultPartGapMm = 5.0;
    public const double DefaultSheetMarginMm = 5.0;
    public const double PreviewSnapThresholdMm = 10.0;
    public const double ComparisonResolution = 0.01;
    public const int DefaultMaxSearchPasses = 80;
    public const int DefaultNoImprovementStopCount = 40;
    public const int DefaultAcceptableMaxUnplaced = -1;
    public const int DefaultAcceptableMaxSheets = -1;
    public const PlacementHeuristic DefaultPlacementHeuristic = PlacementHeuristic.ShortSideFit;
    public const OrientationPreference DefaultOrientationPreference = OrientationPreference.Original;
    public const int DefaultPackStrategyIndex = -1;
    public const bool DefaultEnableGapFilling = true;
    public const bool DefaultAllowMarginRelax = false;

    // DXF contour sampling
    public const int DxfArcSampleCount = 16;
    public const int DxfCircleSampleCount = 32;
    // Tolerance for joining open segments (LINE/ARC/open polyline) into closed loops, mm.
    public const double DxfSegmentJoinToleranceMm = 0.5;

    // Remnant / residual space quality thresholds (mm and mm²)
    public const double RemnantMinUsefulLengthMm = 300.0;
    public const double RemnantMinUsefulWidthMm = 300.0;
    public const double RemnantMaxUsefulAspectRatio = 5.0;
    public const double LongThinWasteAspectRatioThreshold = 5.0;
    public const double LongThinWasteMinDimensionMm = 100.0;
    public const double DeepPocketAreaThresholdMm2 = 100_000.0;
    public const double DeepPocketMinDimensionMm = 100.0;

    // Residual quality weights and penalties (area-equivalent units for penalties)
    public const double ResidualValuableWeight = 1.0;
    public const double ResidualUsefulWeight = 0.5;
    public const double ResidualFragmentationPenaltyMm2 = 50_000.0;
    public const double ResidualLongThinPenaltyMm2 = 100_000.0;
    public const double ResidualDeepPocketPenaltyMm2 = 100_000.0;

    // Visual quality weights (used only as final tie-breaker)
    public const double VisualCompactnessWeight = 0.4;
    public const double VisualRemnantWeight = 0.3;
    public const double VisualFragmentationWeight = 0.2;
    public const double VisualLongThinWeight = 0.1;
}
