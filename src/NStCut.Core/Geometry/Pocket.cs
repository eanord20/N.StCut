namespace NStCut.Core.Geometry;

/// <summary>Class of a connected leftover region on a sheet.</summary>
public enum PocketClass
{
    /// <summary>Large enough to be reused as-is.</summary>
    Valuable,

    /// <summary>Reusable with some limitations.</summary>
    Useful,

    /// <summary>Area is large but the narrow opening makes it hard to use.</summary>
    DeepPocket,

    /// <summary>Long and narrow strip, mostly scrap.</summary>
    LongThin,

    /// <summary>Too small or fragmented to be useful.</summary>
    Scrap
}

/// <summary>A connected residual free region on a sheet, with its best usable rectangle.</summary>
public sealed class Pocket
{
    public Pocket(
        IReadOnlyList<Rect> cells,
        double area,
        Rect largestInscribedRectangle,
        bool isValuable,
        bool isUseful,
        bool isLongThin,
        bool isDeepPocket)
    {
        Cells = cells;
        Area = area;
        LargestInscribedRectangle = largestInscribedRectangle;
        IsValuable = isValuable;
        IsUseful = isUseful;
        IsLongThin = isLongThin;
        IsDeepPocket = isDeepPocket;
    }

    /// <summary>Non-overlapping grid cells that form this pocket.</summary>
    public IReadOnlyList<Rect> Cells { get; }

    /// <summary>Total area of the pocket (union of all cells).</summary>
    public double Area { get; }

    /// <summary>Largest axis-aligned rectangle that fits entirely inside the pocket.</summary>
    public Rect LargestInscribedRectangle { get; }

    public bool IsValuable { get; }
    public bool IsUseful { get; }
    public bool IsLongThin { get; }
    public bool IsDeepPocket { get; }

    public PocketClass Class =>
        IsValuable ? PocketClass.Valuable :
        IsUseful ? PocketClass.Useful :
        IsDeepPocket ? PocketClass.DeepPocket :
        IsLongThin ? PocketClass.LongThin :
        PocketClass.Scrap;
}
