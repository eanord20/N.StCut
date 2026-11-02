using NStCut.Core.Geometry;
using NStCut.Core.Models;

namespace NStCut.Core.Nesting;

/// <summary>
/// Centralizes the orientation policy shared by the evaluator, the UI and all nesting engines.
/// </summary>
public static class OrientationPolicy
{
    /// <summary>
    /// Returns the bounding-box dimensions allowed for a part on the given material.
    /// Order is preserved: original orientation first, then the 90° rotation when allowed.
    /// When the material has grain, the part is forced into the single orientation
    /// that aligns its longer side with the sheet grain direction.
    /// </summary>
    public static IEnumerable<(double Width, double Height)> GetOrientations(Part part, SheetMaterial material, double resolution)
    {
        var w = part.Bounds.Width;
        var h = part.Bounds.Height;

        var followGrain = part.GrainMode == GrainMode.FollowMaterial && material.HasGrain;
        if (followGrain && Math.Abs(w - h) > resolution)
        {
            // Длинная сторона детали должна совпадать с направлением волокна листа:
            // Horizontal — по X (ширина детали), Vertical — по Y (высота детали).
            var longSideHorizontal = w > h;
            var grainHorizontal = material.GrainDirection == GrainDirection.Horizontal;
            yield return longSideHorizontal == grainHorizontal ? (w, h) : (h, w);
            yield break;
        }

        yield return (w, h);

        var allowRotation = part.GrainMode == GrainMode.Allow90 ||
                            (part.GrainMode == GrainMode.FollowMaterial && !material.HasGrain);
        if (allowRotation && Math.Abs(w - h) > resolution)
            yield return (h, w);
    }
}
