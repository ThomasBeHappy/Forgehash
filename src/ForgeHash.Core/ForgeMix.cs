using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace ForgeHash;

/// <summary>
/// ForgeMix compression function: eight full rounds of row, column, and diagonal mixing
/// with a fixed word permutation and XOR feed-forward.
/// </summary>
public static class ForgeMix
{
    /// <summary>Block size in bytes.</summary>
    public const int BlockSize = 1024;

    /// <summary>Words per block.</summary>
    public const int WordsPerBlock = 128;

    /// <summary>Number of full rounds.</summary>
    public const int RoundCount = 8;

    /// <summary>
    /// Compresses a 1024-byte input block with positional metadata into a 1024-byte output block.
    /// </summary>
    public static void Mix(
        ReadOnlySpan<ulong> inputBlock,
        ulong pass,
        ulong lane,
        ulong blockIndex,
        Span<ulong> outputBlock)
    {
        if (inputBlock.Length != WordsPerBlock)
        {
            throw new ArgumentException("Input block must contain 128 words.", nameof(inputBlock));
        }

        if (outputBlock.Length != WordsPerBlock)
        {
            throw new ArgumentException("Output block must contain 128 words.", nameof(outputBlock));
        }

        Span<ulong> state = stackalloc ulong[WordsPerBlock];
        Span<ulong> original = stackalloc ulong[WordsPerBlock];
        Span<ulong> permuted = stackalloc ulong[WordsPerBlock];

        inputBlock.CopyTo(original);
        inputBlock.CopyTo(state);

        InjectPosition(state, pass, lane, blockIndex);

        for (int round = 0; round < RoundCount; round++)
        {
            MixRows(state);
            MixColumns(state);
            MixDiagonals(state);
            PermuteWords(state, permuted);
            permuted.CopyTo(state);
        }

        unchecked
        {
            for (int i = 0; i < WordsPerBlock; i++)
            {
                outputBlock[i] = state[i] ^ original[i];
            }
        }
    }

    /// <summary>
    /// Byte-oriented convenience wrapper that interprets blocks as little-endian UInt64 words.
    /// </summary>
    public static void Mix(
        ReadOnlySpan<byte> inputBlock,
        ulong pass,
        ulong lane,
        ulong blockIndex,
        Span<byte> outputBlock)
    {
        if (inputBlock.Length != BlockSize || outputBlock.Length != BlockSize)
        {
            throw new ArgumentException("Blocks must be exactly 1024 bytes.");
        }

        Span<ulong> inputWords = stackalloc ulong[WordsPerBlock];
        Span<ulong> outputWords = stackalloc ulong[WordsPerBlock];
        BytesToWords(inputBlock, inputWords);
        Mix(inputWords, pass, lane, blockIndex, outputWords);
        WordsToBytes(outputWords, outputBlock);
    }

    /// <summary>
    /// Quarter-round used by row, column, and diagonal mixing.
    /// </summary>
    public static void QuarterRound(ref ulong a, ref ulong b, ref ulong c, ref ulong d)
    {
        unchecked
        {
            a = a + b + (2UL * Low32(a) * Low32(b));
            d = RotateRight64(d ^ a, 32);

            c = c + d + (2UL * Low32(c) * Low32(d));
            b = RotateRight64(b ^ c, 24);

            a = a + b + (2UL * Low32(a) * Low32(b));
            d = RotateRight64(d ^ a, 16);

            c = c + d + (2UL * Low32(c) * Low32(d));
            b = RotateRight64(b ^ c, 63);
        }
    }

    private static void InjectPosition(Span<ulong> state, ulong pass, ulong lane, ulong blockIndex)
    {
        unchecked
        {
            state[0] ^= pass;
            state[1] ^= lane;
            state[2] ^= blockIndex;
            state[3] ^= RotateLeft64(pass + blockIndex, 17);
        }
    }

    private static void MixRows(Span<ulong> state)
    {
        for (int row = 0; row < 8; row++)
        {
            int baseIndex = row * 16;
            ApplySixteenWordSchedule(state, baseIndex);
        }
    }

    private static void MixColumns(Span<ulong> state)
    {
        Span<ulong> virtualRow = stackalloc ulong[16];

        for (int pair = 0; pair < 8; pair++)
        {
            int colA = pair * 2;
            int colB = colA + 1;

            for (int row = 0; row < 8; row++)
            {
                virtualRow[row * 2] = state[Index(row, colA)];
                virtualRow[row * 2 + 1] = state[Index(row, colB)];
            }

            ApplySixteenWordSchedule(virtualRow, 0);

            for (int row = 0; row < 8; row++)
            {
                state[Index(row, colA)] = virtualRow[row * 2];
                state[Index(row, colB)] = virtualRow[row * 2 + 1];
            }
        }
    }

    private static void MixDiagonals(Span<ulong> state)
    {
        Span<ulong> group = stackalloc ulong[16];

        for (int diagonalIndex = 0; diagonalIndex < 8; diagonalIndex++)
        {
            int @base = diagonalIndex * 2;

            for (int k = 0; k < 8; k++)
            {
                group[k * 2] = state[Index(k, (@base + k) & 15)];
                group[k * 2 + 1] = state[Index(k, (@base + k + 8) & 15)];
            }

            ApplySixteenWordSchedule(group, 0);

            for (int k = 0; k < 8; k++)
            {
                state[Index(k, (@base + k) & 15)] = group[k * 2];
                state[Index(k, (@base + k + 8) & 15)] = group[k * 2 + 1];
            }
        }
    }

    /// <summary>
    /// Applies the fixed eight quarter-round schedule used for every 16-word group.
    /// </summary>
    private static void ApplySixteenWordSchedule(Span<ulong> words, int offset)
    {
        QuarterRound(ref words[offset + 0], ref words[offset + 4], ref words[offset + 8], ref words[offset + 12]);
        QuarterRound(ref words[offset + 1], ref words[offset + 5], ref words[offset + 9], ref words[offset + 13]);
        QuarterRound(ref words[offset + 2], ref words[offset + 6], ref words[offset + 10], ref words[offset + 14]);
        QuarterRound(ref words[offset + 3], ref words[offset + 7], ref words[offset + 11], ref words[offset + 15]);

        QuarterRound(ref words[offset + 0], ref words[offset + 5], ref words[offset + 10], ref words[offset + 15]);
        QuarterRound(ref words[offset + 1], ref words[offset + 6], ref words[offset + 11], ref words[offset + 12]);
        QuarterRound(ref words[offset + 2], ref words[offset + 7], ref words[offset + 8], ref words[offset + 13]);
        QuarterRound(ref words[offset + 3], ref words[offset + 4], ref words[offset + 9], ref words[offset + 14]);
    }

    private static void PermuteWords(ReadOnlySpan<ulong> state, Span<ulong> permuted)
    {
        for (int sourceIndex = 0; sourceIndex < WordsPerBlock; sourceIndex++)
        {
            int destinationIndex = ((sourceIndex * 73) + 19) & 127;
            permuted[destinationIndex] = state[sourceIndex];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Index(int row, int column) => (row * 16) + column;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Low32(ulong value) => value & 0xFFFF_FFFFUL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotateRight64(ulong value, int bits)
        => (value >> bits) | (value << (64 - bits));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotateLeft64(ulong value, int bits)
        => (value << bits) | (value >> (64 - bits));

    internal static void BytesToWords(ReadOnlySpan<byte> bytes, Span<ulong> words)
    {
        for (int i = 0; i < WordsPerBlock; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(i * 8, 8));
        }
    }

    internal static void WordsToBytes(ReadOnlySpan<ulong> words, Span<byte> bytes)
    {
        for (int i = 0; i < WordsPerBlock; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(i * 8, 8), words[i]);
        }
    }
}
