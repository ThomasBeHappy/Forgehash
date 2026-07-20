using System.Buffers.Binary;
using System.Text;

namespace ForgeHash.Internal;

/// <summary>
/// Little-endian binary helpers used by ForgeHash input encoding and metadata fields.
/// </summary>
internal static class BinaryEncoding
{
    internal static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);
    }

    internal static void WriteUInt64(Span<byte> destination, int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, 8), value);
    }

    internal static void WriteInt32(Span<byte> destination, int offset, int value)
        => WriteUInt32(destination, offset, unchecked((uint)value));

    internal static void WriteInt64(Span<byte> destination, int offset, long value)
        => WriteUInt64(destination, offset, unchecked((ulong)value));

    /// <summary>
    /// Builds the unambiguous binary input buffer described in the specification §9.
    /// </summary>
    internal static byte[] BuildEncodedInput(
        int version,
        int memoryKiB,
        int iterations,
        int parallelism,
        int outputLength,
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> context)
    {
        checked
        {
            int length =
                4 + 4 + 4 + 4 + 4 +
                8 + password.Length +
                4 + salt.Length +
                4 + context.Length;

            byte[] buffer = new byte[length];
            int offset = 0;

            WriteInt32(buffer, offset, version);
            offset += 4;
            WriteInt32(buffer, offset, memoryKiB);
            offset += 4;
            WriteInt32(buffer, offset, iterations);
            offset += 4;
            WriteInt32(buffer, offset, parallelism);
            offset += 4;
            WriteInt32(buffer, offset, outputLength);
            offset += 4;

            WriteInt64(buffer, offset, password.Length);
            offset += 8;
            password.CopyTo(buffer.AsSpan(offset));
            offset += password.Length;

            WriteInt32(buffer, offset, salt.Length);
            offset += 4;
            salt.CopyTo(buffer.AsSpan(offset));
            offset += salt.Length;

            WriteInt32(buffer, offset, context.Length);
            offset += 4;
            context.CopyTo(buffer.AsSpan(offset));

            return buffer;
        }
    }

    internal static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
