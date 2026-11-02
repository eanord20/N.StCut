using System.Linq;

using NStCut.Core.Models;
using NStCut.Nesting.Services;

namespace NStCut.Nesting.Internals;

/// <summary>
/// Reproduces the part ordering used by <see cref="BoundingBoxShelfEngine"/> and provides
/// a deterministic canonical hash for the sorted part list. This lets the multi-pass engine
/// skip passes that collapse to the same input.
/// </summary>
internal static class PackingSorter
{
    public static IReadOnlyList<(int CopyIndex, Part Part, int StableIndex)> Sort(
        IReadOnlyList<(int CopyIndex, Part Part, int StableIndex)> copies,
        PackingStrategy strategy)
    {
        return strategy switch
        {
            PackingStrategy.AsGiven => copies
                .OrderBy(x => x.StableIndex)
                .ThenBy(x => x.CopyIndex)
                .ThenBy(x => x.Part.Number)
                .ThenBy(x => x.Part.Title)
                .ToList(),
            PackingStrategy.MaxSide => copies
                .OrderByDescending(x => Math.Max(x.Part.Bounds.Width, x.Part.Bounds.Height))
                .ThenBy(x => x.StableIndex)
                .ThenBy(x => x.CopyIndex)
                .ThenBy(x => x.Part.Number)
                .ThenBy(x => x.Part.Title)
                .ToList(),
            PackingStrategy.Area => copies
                .OrderByDescending(x => x.Part.Bounds.Area)
                .ThenBy(x => x.StableIndex)
                .ThenBy(x => x.CopyIndex)
                .ThenBy(x => x.Part.Number)
                .ThenBy(x => x.Part.Title)
                .ToList(),
            PackingStrategy.Width => copies
                .OrderByDescending(x => x.Part.Bounds.Width)
                .ThenBy(x => x.StableIndex)
                .ThenBy(x => x.CopyIndex)
                .ThenBy(x => x.Part.Number)
                .ThenBy(x => x.Part.Title)
                .ToList(),
            PackingStrategy.Height => copies
                .OrderByDescending(x => x.Part.Bounds.Height)
                .ThenBy(x => x.StableIndex)
                .ThenBy(x => x.CopyIndex)
                .ThenBy(x => x.Part.Number)
                .ThenBy(x => x.Part.Title)
                .ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };
    }

    public static long ComputeSortedHash(IReadOnlyList<Part> parts, PackingStrategy strategy)
    {
        var expanded = parts
            .SelectMany((part, stableIndex) => Enumerable.Range(1, Math.Max(0, part.Quantity))
                .Select(copyIndex => (CopyIndex: copyIndex, Part: part, StableIndex: stableIndex)))
            .ToList();

        var sorted = Sort(expanded, strategy);

        var hash = StableHash.FnvOffsetBasis;
        foreach (var (copyIndex, part, stableIndex) in sorted)
        {
            hash = StableHash.Add(hash, part.Number ?? string.Empty);
            hash = StableHash.Add(hash, part.Title ?? string.Empty);
            hash = StableHash.Add(hash, part.Quantity);
            hash = StableHash.Add(hash, part.MaterialId ?? string.Empty);
            hash = StableHash.Add(hash, (int)part.GrainMode);
            hash = StableHash.Add(hash, part.Bounds.X);
            hash = StableHash.Add(hash, part.Bounds.Y);
            hash = StableHash.Add(hash, part.Bounds.Width);
            hash = StableHash.Add(hash, part.Bounds.Height);
            hash = StableHash.Add(hash, part.Area);
            hash = StableHash.Add(hash, copyIndex);
            hash = StableHash.Add(hash, stableIndex);
        }

        return (long)hash;
    }
}
