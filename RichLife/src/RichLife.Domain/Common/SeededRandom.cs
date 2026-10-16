namespace RichLife.Domain.Common;

/// <summary>
/// SplitMix64. <see cref="Random"/>'s seeded sequence is not guaranteed to stay the same
/// across .NET versions, and seeded draws (loan offers, unit breakdowns) must not change
/// when the runtime is upgraded.
/// </summary>
internal sealed class SeededRandom(ulong seed)
{
    private ulong _state = seed;

    /// <summary>A seed from a Guid and a counter — the same pair always gives the same draws.</summary>
    public static ulong Seed(Guid id, int counter)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        var a = BitConverter.ToUInt64(bytes[..8]);
        var b = BitConverter.ToUInt64(bytes[8..]);
        return a ^ (b * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)counter * 0xD1B54A32D192ED03UL);
    }

    private ulong NextULong()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>0 ≤ n &lt; <paramref name="maxExclusive"/>.</summary>
    public int Next(int maxExclusive) => (int)(NextULong() % (ulong)maxExclusive);

    /// <summary>0 ≤ x &lt; 1.</summary>
    public decimal NextUnit() => (decimal)(NextULong() >> 11) / (1UL << 53);
}
