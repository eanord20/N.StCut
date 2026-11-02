using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;

namespace NStCut.Nesting.Internals;

internal static class OrientationHelper
{
    public static IEnumerable<(double Width, double Height)> GetOrientations(Part part, SheetMaterial material, double resolution)
        => OrientationPolicy.GetOrientations(part, material, resolution);
}
