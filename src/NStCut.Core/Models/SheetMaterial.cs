namespace NStCut.Core.Models;

public sealed class SheetMaterial
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public double Length { get; set; }
    public double Width { get; set; }
    public double? Thickness { get; set; }
    public bool HasGrain { get; set; }
    public GrainDirection GrainDirection { get; set; }
}
