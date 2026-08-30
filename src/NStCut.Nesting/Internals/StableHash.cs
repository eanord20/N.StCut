using NStCut.Core.Models;

namespace NStCut.Nesting.Internals;

/// <summary>
/// Stable, deterministic 64-bit hash for nesting inputs.
/// The same inputs always produce the same hash; small input changes are unlikely to collide.
/// </summary>
internal static class StableHash
{
    private const ulong FnvOffsetBasis = 14695981039346656037;
    private const ulong FnvPrime = 1099511628211;

    public static long Compute(IReadOnlyList<Part> parts, IReadOnlyList<SheetMaterial> materials, NestingOptions options)
    {
        var hash = FnvOffsetBasis;

        hash = Add(hash, parts.Count);
        foreach (var part in parts)
        {
            hash = Add(hash, part.Number ?? string.Empty);
            hash = Add(hash, part.Title ?? string.Empty);
            hash = Add(hash, part.Quantity);
            hash = Add(hash, part.MaterialId ?? string.Empty);
            hash = Add(hash, (int)part.GrainMode);
            hash = Add(hash, part.Bounds.X);
            hash = Add(hash, part.Bounds.Y);
            hash = Add(hash, part.Bounds.Width);
            hash = Add(hash, part.Bounds.Height);
        }

        hash = Add(hash, materials.Count);
        foreach (var material in materials)
        {
            hash = Add(hash, material.Id);
            hash = Add(hash, material.Name ?? string.Empty);
            hash = Add(hash, material.Length);
            hash = Add(hash, material.Width);
            hash = Add(hash, material.Thickness.GetValueOrDefault());
            hash = Add(hash, material.HasGrain);
            hash = Add(hash, (int)material.GrainDirection);
        }

        hash = Add(hash, options.PartGapMm);
        hash = Add(hash, options.SheetMarginMm);
        hash = Add(hash, options.Resolution);
        hash = Add(hash, options.MaxSearchPasses);
        hash = Add(hash, options.NoImprovementStopCount);

        return (long)hash;
    }

    private static ulong Add(ulong hash, string value)
    {
        hash = Add(hash, value.Length);
        foreach (var c in value)
            hash = Add(hash, c);
        return hash;
    }

    private static ulong Add(ulong hash, int value)
    {
        hash ^= (ulong)value;
        return unchecked(hash * FnvPrime);
    }

    private static ulong Add(ulong hash, long value)
    {
        hash ^= (ulong)value;
        return unchecked(hash * FnvPrime);
    }

    private static ulong Add(ulong hash, double value)
        => Add(hash, BitConverter.DoubleToInt64Bits(value));

    private static ulong Add(ulong hash, char value)
        => Add(hash, (int)value);

    private static ulong Add(ulong hash, bool value)
        => Add(hash, value ? 1 : 0);
}
