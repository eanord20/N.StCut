using NStCut.Core.Geometry;
using NStCut.Core.Models;

namespace NStCut.Nesting.Internals;

internal static class OrientationHelper
{
    public static IEnumerable<(double Width, double Height)> GetOrientations(Part part, SheetMaterial material)
    {
        var w = part.Bounds.Width;
        var h = part.Bounds.Height;

        var allowRotation = part.GrainMode == GrainMode.Allow90 ||
                            (part.GrainMode == GrainMode.FollowMaterial && !material.HasGrain);

        yield return (w, h);

        if (allowRotation && Math.Abs(w - h) > 0.001)
            yield return (h, w);
    }
}
