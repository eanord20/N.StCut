using NStCut.Core.Models;

namespace NStCut.Nesting.Internals;

/// <summary>
/// Generates deterministic permutations of the part list.
/// The same base seed and pass number always produce the same permutation.
/// </summary>
internal static class DeterministicPermutationSource
{
    public static IReadOnlyList<Part> Permute(IReadOnlyList<Part> source, long baseSeed, int pass)
    {
        if (source.Count <= 1)
            return new List<Part>(source);

        var result = new List<Part>(source);

        var seedRng = new DeterministicRandom(baseSeed);
        for (var i = 0; i <= pass; i++)
            seedRng.Next(int.MaxValue);

        var rng = new DeterministicRandom(seedRng.Next(int.MaxValue));

        for (var i = result.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }
}
