namespace NStCut.Core.Models;

public sealed class NestingOptions
{
    public double PartGapMm { get; set; } = 5.0;
    public double SheetMarginMm { get; set; } = 5.0;
    public double Resolution { get; set; } = 0.01;
}
