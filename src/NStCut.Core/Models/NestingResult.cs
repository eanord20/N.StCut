namespace NStCut.Core.Models;

public sealed class NestingResult
{
    public List<SheetInstance> Sheets { get; } = new();
    public List<Part> Unplaced { get; } = new();
    public double TotalUtilizationPercent { get; set; }
}
