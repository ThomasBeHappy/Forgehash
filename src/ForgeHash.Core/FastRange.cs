namespace ForgeHash;

/// <summary>
/// Bias-resistant mapping of a 64-bit value into <c>[0, n)</c> via multiply-high.
/// </summary>
public static class FastRange
{
    /// <summary>
    /// Returns the high 64 bits of the 128-bit product <c>x * n</c>.
    /// </summary>
    /// <param name="x">Uniform 64-bit input word.</param>
    /// <param name="n">Exclusive upper bound. Must be greater than zero.</param>
    public static ulong Map(ulong x, ulong n)
    {
        if (n == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "Range size must be greater than zero.");
        }

        return MultiplyHigh(x, n);
    }

    /// <summary>
    /// Convenience overload for integer lane and block counts.
    /// </summary>
    public static int Map(ulong x, int n)
    {
        if (n <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n), "Range size must be greater than zero.");
        }

        return (int)MultiplyHigh(x, (ulong)n);
    }

    private static ulong MultiplyHigh(ulong x, ulong y)
    {
        // Portable 64×64→128 multiply-high without relying on UInt128 availability differences.
        ulong xLow = (uint)x;
        ulong xHigh = x >> 32;
        ulong yLow = (uint)y;
        ulong yHigh = y >> 32;

        ulong lowLow = xLow * yLow;
        ulong lowHigh = xLow * yHigh;
        ulong highLow = xHigh * yLow;
        ulong highHigh = xHigh * yHigh;

        ulong mid = (lowLow >> 32) + (uint)lowHigh + (uint)highLow;
        ulong carry = mid >> 32;

        return highHigh + (lowHigh >> 32) + (highLow >> 32) + carry;
    }
}
