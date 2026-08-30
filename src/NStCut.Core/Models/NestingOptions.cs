using NStCut.Core.Constants;

namespace NStCut.Core.Models;

public sealed class NestingOptions
{
    public double PartGapMm { get; set; } = AppConstants.DefaultPartGapMm;
    public double SheetMarginMm { get; set; } = AppConstants.DefaultSheetMarginMm;
    public double Resolution { get; set; } = AppConstants.ComparisonResolution;
    public int MaxSearchPasses { get; set; } = AppConstants.DefaultMaxSearchPasses;
    public int NoImprovementStopCount { get; set; } = AppConstants.DefaultNoImprovementStopCount;
}
