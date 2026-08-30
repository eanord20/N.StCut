namespace NStCut.Nesting.Internals;

/// <summary>
/// Deterministic 64-bit xorshift pseudo-random generator.
/// Algorithm does not depend on the .NET <see cref="System.Random"/> implementation,
/// which makes results reproducible across framework updates.
/// </summary>
internal sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(long seed)
    {
        var s = (ulong)seed;
        _state = s == 0 ? 1 : s;
    }

    public int Next(int maxValue)
    {
        _state ^= _state << 13;
        _state ^= _state >> 7;
        _state ^= _state << 17;

        return (int)(_state % (ulong)maxValue);
    }
}
