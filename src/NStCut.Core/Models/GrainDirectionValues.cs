namespace NStCut.Core.Models;

public static class GrainDirectionValues
{
    public static IReadOnlyList<GrainDirection> Values { get; } = new[] { GrainDirection.Horizontal, GrainDirection.Vertical };
}
