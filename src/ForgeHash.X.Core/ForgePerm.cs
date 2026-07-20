namespace ForgeHashX;

/// <summary>
/// ForgePerm — 16-word ARX permutation for the ForgeX sponge (SPECIFICATION_X §4.3).
/// </summary>
internal static class ForgePerm
{
    public const int Words = 16;
    public const int Rounds = 8;

    public static void Permute(Span<ulong> state)
    {
        if (state.Length != Words)
        {
            throw new ArgumentException("State must be 16 words.", nameof(state));
        }

        Span<ulong> temp = stackalloc ulong[Words];
        for (int r = 0; r < Rounds; r++)
        {
            for (int i = 0; i < Words; i++)
            {
                state[i] ^= RoundConstant(r, i);
            }

            QuarterRound(state, 0, 4, 8, 12);
            QuarterRound(state, 1, 5, 9, 13);
            QuarterRound(state, 2, 6, 10, 14);
            QuarterRound(state, 3, 7, 11, 15);

            QuarterRound(state, 0, 5, 10, 15);
            QuarterRound(state, 1, 6, 11, 12);
            QuarterRound(state, 2, 7, 8, 13);
            QuarterRound(state, 3, 4, 9, 14);

            for (int i = 0; i < Words; i++)
            {
                temp[(i * 7 + 3) & 15] = state[i];
            }

            temp.CopyTo(state);
        }
    }

    internal static ulong RoundConstant(int round, int index)
    {
        unchecked
        {
            ulong x = 0x9E3779B97F4A7C15UL
                      ^ ((ulong)(uint)round * 0xD1B54A32D192ED03UL)
                      ^ ((ulong)(uint)index * 0xA24BAED4963EE407UL);
            int rot = (round + index * 3) & 63;
            return Rotl(x, rot);
        }
    }

    private static void QuarterRound(Span<ulong> s, int ia, int ib, int ic, int id)
    {
        unchecked
        {
            ulong a = s[ia];
            ulong b = s[ib];
            ulong c = s[ic];
            ulong d = s[id];

            a = a + b + 2UL * Low32(a) * Low32(b);
            d = Rotr(d ^ a, 17);
            c = c + d + 2UL * Low32(c) * Low32(d);
            b = Rotr(b ^ c, 11);
            a = a + b + 2UL * Low32(a) * Low32(b);
            d = Rotr(d ^ a, 23);
            c = c + d + 2UL * Low32(c) * Low32(d);
            b = Rotr(b ^ c, 41);

            s[ia] = a;
            s[ib] = b;
            s[ic] = c;
            s[id] = d;
        }
    }

    private static ulong Low32(ulong x) => x & 0xFFFFFFFFUL;

    internal static ulong Rotl(ulong x, int n)
    {
        n &= 63;
        return n == 0 ? x : (x << n) | (x >> (64 - n));
    }

    internal static ulong Rotr(ulong x, int n)
    {
        n &= 63;
        return n == 0 ? x : (x >> n) | (x << (64 - n));
    }
}
