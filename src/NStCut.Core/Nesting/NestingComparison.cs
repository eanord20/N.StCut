namespace NStCut.Core.Nesting;

/// <summary>Shared comparisons for physical fit decisions across evaluators and nesting engines.</summary>
public static class NestingComparison
{
    /// <summary>Physical placement is strict: resolution must never extend a sheet or free rectangle.</summary>
    public static bool FitsWithin(double value, double limit) => value <= limit;
}
