namespace NStCut.Core.Nesting;

/// <summary>
/// Heuristic used by the MaxRects packer to choose where to place the next part.
/// </summary>
public enum PlacementHeuristic
{
    /// <summary>Prefer the free rectangle with the smallest short-side leftover.</summary>
    ShortSideFit,

    /// <summary>Prefer the free rectangle with the smallest long-side leftover.</summary>
    LongSideFit,

    /// <summary>Prefer the free rectangle that is closest in area to the part.</summary>
    BestAreaFit,

    /// <summary>Prefer the lowest and leftmost free rectangle.</summary>
    BottomLeft
}
