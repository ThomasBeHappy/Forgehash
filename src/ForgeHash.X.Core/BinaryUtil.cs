using System.Buffers.Binary;

namespace ForgeHashX;

internal static class BinaryUtil
{
    public static void WriteU32(Span<byte> dest, int offset, int value)
        => BinaryPrimitives.WriteInt32LittleEndian(dest.Slice(offset), value);

    public static void WriteU64(Span<byte> dest, int offset, long value)
        => BinaryPrimitives.WriteInt64LittleEndian(dest.Slice(offset), value);

    public static ulong FastRange(ulong x, ulong n)
    {
        UInt128 product = (UInt128)x * n;
        return (ulong)(product >> 64);
    }

    public static void WordsToBytes(ReadOnlySpan<ulong> words, Span<byte> bytes)
    {
        for (int i = 0; i < words.Length; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(i * 8), words[i]);
        }
    }

    public static void BytesToWords(ReadOnlySpan<byte> bytes, Span<ulong> words)
    {
        for (int i = 0; i < words.Length; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(i * 8));
        }
    }
}
