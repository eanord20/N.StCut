namespace NStCut.Core.Nesting;

/// <summary>
/// Which part orientation the packer should prefer when both fit into the same free rectangle.
/// </summary>
public enum OrientationPreference
{
    /// <summary>Prefer the original orientation (OrientationOrder = 0).</summary>
    Original,

    /// <summary>Prefer the 90°-rotated orientation (OrientationOrder = 1).</summary>
    Rotated
}
