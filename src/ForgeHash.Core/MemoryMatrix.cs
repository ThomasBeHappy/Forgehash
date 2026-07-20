using System.Security.Cryptography;

namespace ForgeHash;

/// <summary>
/// Contiguous memory matrix of 1024-byte blocks stored as little-endian UInt64 words.
/// </summary>
internal sealed class MemoryMatrix : IDisposable
{
    private ulong[]? _words;
    private bool _disposed;

    public MemoryMatrix(int blockCount, int parallelism)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parallelism);

        if (blockCount % parallelism != 0)
        {
            throw new ArgumentException("Block count must be evenly divisible by parallelism.", nameof(blockCount));
        }

        BlocksPerLane = blockCount / parallelism;
        if (BlocksPerLane % 4 != 0)
        {
            throw new ArgumentException("Blocks per lane must be divisible by 4.", nameof(blockCount));
        }

        checked
        {
            int wordCount = blockCount * ForgeMix.WordsPerBlock;
            _words = new ulong[wordCount];
        }

        BlockCount = blockCount;
        Parallelism = parallelism;
        SliceLength = BlocksPerLane / 4;
    }

    public int BlockCount { get; }

    public int Parallelism { get; }

    public int BlocksPerLane { get; }

    public int SliceLength { get; }

    public Span<ulong> GetBlock(int lane, int blockIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateIndices(lane, blockIndex);
        int flatIndex = GetFlatIndex(lane, blockIndex);
        return _words!.AsSpan(flatIndex * ForgeMix.WordsPerBlock, ForgeMix.WordsPerBlock);
    }

    public ReadOnlySpan<ulong> GetBlockReadOnly(int lane, int blockIndex)
        => GetBlock(lane, blockIndex);

    public int GetFlatIndex(int lane, int blockIndex)
    {
        ValidateIndices(lane, blockIndex);
        checked
        {
            return (lane * BlocksPerLane) + blockIndex;
        }
    }

    public void XorBlocks(Span<ulong> destination, ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right)
    {
        if (destination.Length != ForgeMix.WordsPerBlock ||
            left.Length != ForgeMix.WordsPerBlock ||
            right.Length != ForgeMix.WordsPerBlock)
        {
            throw new ArgumentException("All blocks must contain 128 words.");
        }

        unchecked
        {
            for (int i = 0; i < ForgeMix.WordsPerBlock; i++)
            {
                destination[i] = left[i] ^ right[i];
            }
        }
    }

    public void Clear()
    {
        if (_words is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(_words.AsSpan()));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _words = null;
        _disposed = true;
    }

    private void ValidateIndices(int lane, int blockIndex)
    {
        if ((uint)lane >= (uint)Parallelism)
        {
            throw new ArgumentOutOfRangeException(nameof(lane));
        }

        if ((uint)blockIndex >= (uint)BlocksPerLane)
        {
            throw new ArgumentOutOfRangeException(nameof(blockIndex));
        }
    }
}
