using NStCut.Core.Models;
using NStCut.Nesting.Internals;

namespace NStCut.Nesting.Tests;

public class DeterministicPermutationSourceTests
{
    [Fact]
    public void Permute_IsReproducible()
    {
        var source = new[]
        {
            new Part { Number = "A" },
            new Part { Number = "B" },
            new Part { Number = "C" }
        };

        var first = DeterministicPermutationSource.Permute(source, 12345L, 2);
        var second = DeterministicPermutationSource.Permute(source, 12345L, 2);

        Assert.Equal(first.Select(p => p.Number).ToList(), second.Select(p => p.Number).ToList());
    }

    [Fact]
    public void Permute_DifferentPassProducesDifferentOrder()
    {
        var source = new[]
        {
            new Part { Number = "A" },
            new Part { Number = "B" },
            new Part { Number = "C" },
            new Part { Number = "D" },
            new Part { Number = "E" }
        };

        var pass0 = DeterministicPermutationSource.Permute(source, 999L, 0).Select(p => p.Number).ToList();
        var pass1 = DeterministicPermutationSource.Permute(source, 999L, 1).Select(p => p.Number).ToList();

        Assert.NotEqual(pass0, pass1);
    }

    [Fact]
    public void Permute_ContainsSameElements()
    {
        var source = new[]
        {
            new Part { Number = "A" },
            new Part { Number = "B" },
            new Part { Number = "C" }
        };

        var permuted = DeterministicPermutationSource.Permute(source, 42L, 5);

        Assert.Equal(3, permuted.Count);
        Assert.Equal(new[] { "A", "B", "C" }.OrderBy(x => x).ToList(),
                     permuted.Select(p => p.Number).OrderBy(x => x).ToList());
    }

    [Fact]
    public void Permute_SinglePartIsUnchanged()
    {
        var source = new[] { new Part { Number = "A" } };

        var permuted = DeterministicPermutationSource.Permute(source, 123L, 7);

        Assert.Single(permuted);
        Assert.Equal("A", permuted[0].Number);
    }

    [Fact]
    public void Permute_MultiplePassesYieldMultipleDistinctOrders()
    {
        var source = new[]
        {
            new Part { Number = "A" },
            new Part { Number = "B" },
            new Part { Number = "C" },
            new Part { Number = "D" },
            new Part { Number = "E" }
        };

        var distinct = Enumerable.Range(0, 10)
            .Select(pass => DeterministicPermutationSource.Permute(source, 999L, pass))
            .Select(p => string.Concat(p.Select(x => x.Number)))
            .Distinct()
            .ToList();

        Assert.True(distinct.Count > 5, $"Expected more than 5 distinct permutations, got {distinct.Count}");
    }
}
