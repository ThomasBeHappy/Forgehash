using System.Buffers.Binary;
using System.Security.Cryptography;
using Blake3;
using ForgeHash.Internal;

namespace ForgeHash;

/// <summary>
/// Single-threaded ForgeHash-B3 version 1 reference engine.
/// </summary>
internal static class ForgeHashEngine
{
    internal const int AlgorithmVersion = 1;

    internal static byte[] DeriveHash(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashParameters parameters,
        bool useParallelLanes = false,
        BlockReferenceObserver? observer = null)
    {
        ParameterValidator.ValidatePasswordLength(password.Length);
        ParameterValidator.ValidateForHashing(parameters);

        if (salt.Length < ForgeHashParameters.MinimumSaltLength ||
            salt.Length > ForgeHashParameters.MaximumSaltLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(salt),
                $"Salt length must be between {ForgeHashParameters.MinimumSaltLength} and {ForgeHashParameters.MaximumSaltLength} bytes.");
        }

        byte[] encodedInput = BinaryEncoding.BuildEncodedInput(
            AlgorithmVersion,
            parameters.MemoryKiB,
            parameters.Iterations,
            parameters.Parallelism,
            parameters.OutputLength,
            password,
            salt,
            ReadOnlySpan<byte>.Empty);

        byte[]? seed = null;
        try
        {
            seed = Blake3Adapter.DeriveSeed(encodedInput);
            using MemoryMatrix memory = new(parameters.MemoryKiB, parameters.Parallelism);
            InitializeMemory(memory, seed);

            if (useParallelLanes && parameters.Parallelism > 1)
            {
                FillMemoryParallel(memory, parameters.Iterations, observer);
            }
            else
            {
                FillMemorySequential(memory, parameters.Iterations, observer);
            }

            return Finalize(memory, seed, parameters);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encodedInput);
            if (seed is not null)
            {
                CryptographicOperations.ZeroMemory(seed);
            }
        }
    }

    /// <summary>
    /// Derives the 32-byte seed for diagnostics and test vectors.
    /// </summary>
    internal static byte[] ComputeSeed(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashParameters parameters)
    {
        ParameterValidator.ValidatePasswordLength(password.Length);
        ParameterValidator.ValidateForHashing(parameters);

        byte[] encodedInput = BinaryEncoding.BuildEncodedInput(
            AlgorithmVersion,
            parameters.MemoryKiB,
            parameters.Iterations,
            parameters.Parallelism,
            parameters.OutputLength,
            password,
            salt,
            ReadOnlySpan<byte>.Empty);

        try
        {
            return Blake3Adapter.DeriveSeed(encodedInput);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encodedInput);
        }
    }

    internal static void InitializeMemoryPublic(MemoryMatrix memory, ReadOnlySpan<byte> seed)
        => InitializeMemory(memory, seed);

    internal static void FillMemorySequentialPublic(
        MemoryMatrix memory,
        int iterations,
        BlockReferenceObserver? observer,
        ForgeMixSampleObserver? mixObserver = null)
        => FillMemorySequential(memory, iterations, observer, mixObserver);

    internal static byte[] ComputeGroupRootPublic(MemoryMatrix memory)
        => ComputeGroupRoot(memory);

    internal static byte[] FinalizePublic(
        MemoryMatrix memory,
        ReadOnlySpan<byte> seed,
        ForgeHashParameters parameters,
        out byte[] groupRoot)
    {
        groupRoot = ComputeGroupRoot(memory);
        return FinalizeFromParts(memory, seed, parameters, groupRoot);
    }

    private static void InitializeMemory(MemoryMatrix memory, ReadOnlySpan<byte> seed)
    {
        Span<byte> expandInput = stackalloc byte[seed.Length + 8];
        seed.CopyTo(expandInput);
        Span<byte> blockBytes = stackalloc byte[ForgeMix.BlockSize];
        Span<ulong> blockWords = stackalloc ulong[ForgeMix.WordsPerBlock];

        for (int lane = 0; lane < memory.Parallelism; lane++)
        {
            for (int blockIndex = 0; blockIndex < 2; blockIndex++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(expandInput.Slice(seed.Length, 4), (uint)lane);
                BinaryPrimitives.WriteUInt32LittleEndian(expandInput.Slice(seed.Length + 4, 4), (uint)blockIndex);
                Blake3Adapter.Expand(expandInput, blockBytes);
                ForgeMix.BytesToWords(blockBytes, blockWords);
                blockWords.CopyTo(memory.GetBlock(lane, blockIndex));
            }
        }

        CryptographicOperations.ZeroMemory(expandInput);
        CryptographicOperations.ZeroMemory(blockBytes);
        CryptographicOperations.ZeroMemory(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(blockWords));
    }

    private static void FillMemorySequential(
        MemoryMatrix memory,
        int iterations,
        BlockReferenceObserver? observer,
        ForgeMixSampleObserver? mixObserver = null)
    {
        Span<ulong> combined = stackalloc ulong[ForgeMix.WordsPerBlock];
        Span<ulong> mixed = stackalloc ulong[ForgeMix.WordsPerBlock];

        for (int pass = 0; pass < iterations; pass++)
        {
            for (int slice = 0; slice < 4; slice++)
            {
                for (int lane = 0; lane < memory.Parallelism; lane++)
                {
                    ProcessSlice(memory, pass, slice, lane, combined, mixed, observer, mixObserver);
                }
            }
        }

        CryptographicOperations.ZeroMemory(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(combined));
        CryptographicOperations.ZeroMemory(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(mixed));
    }

    private static void FillMemoryParallel(
        MemoryMatrix memory,
        int iterations,
        BlockReferenceObserver? observer)
    {
        // Parallel lanes with a barrier after each slice. Workers never write the same lane.
        // Use heap buffers here because stackalloc is not valid inside Parallel.For delegates.
        for (int pass = 0; pass < iterations; pass++)
        {
            for (int slice = 0; slice < 4; slice++)
            {
                int capturedPass = pass;
                int capturedSlice = slice;
                System.Threading.Tasks.Parallel.For(0, memory.Parallelism, lane =>
                {
                    ulong[] combined = new ulong[ForgeMix.WordsPerBlock];
                    ulong[] mixed = new ulong[ForgeMix.WordsPerBlock];
                    try
                    {
                        ProcessSlice(memory, capturedPass, capturedSlice, lane, combined, mixed, observer, mixObserver: null);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(
                            System.Runtime.InteropServices.MemoryMarshal.AsBytes(combined.AsSpan()));
                        CryptographicOperations.ZeroMemory(
                            System.Runtime.InteropServices.MemoryMarshal.AsBytes(mixed.AsSpan()));
                    }
                });
            }
        }
    }

    private static void ProcessSlice(
        MemoryMatrix memory,
        int pass,
        int slice,
        int lane,
        Span<ulong> combined,
        Span<ulong> mixed,
        BlockReferenceObserver? observer,
        ForgeMixSampleObserver? mixObserver)
    {
        int start = slice * memory.SliceLength;
        int end = start + memory.SliceLength;

        // Pass zero already initialized blocks 0 and 1.
        if (pass == 0 && slice == 0)
        {
            start = 2;
        }

        for (int blockIndex = start; blockIndex < end; blockIndex++)
        {
            int previousIndex = blockIndex > 0 ? blockIndex - 1 : memory.BlocksPerLane - 1;
            ReadOnlySpan<ulong> previous = memory.GetBlockReadOnly(lane, previousIndex);

            SelectReference(
                memory,
                pass,
                slice,
                lane,
                blockIndex,
                previous,
                out int referenceLane,
                out int referenceIndex);

            ReadOnlySpan<ulong> reference = memory.GetBlockReadOnly(referenceLane, referenceIndex);
            memory.XorBlocks(combined, previous, reference);

            observer?.Invoke(new BlockReferenceTrace(
                pass,
                slice,
                lane,
                blockIndex,
                previousIndex,
                referenceLane,
                referenceIndex,
                referenceLane != lane));

            if (pass == 0)
            {
                Span<ulong> destination = memory.GetBlock(lane, blockIndex);
                ForgeMix.Mix(combined, (ulong)pass, (ulong)lane, (ulong)blockIndex, destination);
                mixObserver?.Invoke(pass, lane, blockIndex, destination);
            }
            else
            {
                Span<ulong> current = memory.GetBlock(lane, blockIndex);
                ForgeMix.Mix(combined, (ulong)pass, (ulong)lane, (ulong)blockIndex, mixed);
                mixObserver?.Invoke(pass, lane, blockIndex, mixed);
                memory.XorBlocks(current, current, mixed);
            }
        }
    }

    private static void SelectReference(
        MemoryMatrix memory,
        int pass,
        int slice,
        int currentLane,
        int blockIndex,
        ReadOnlySpan<ulong> previous,
        out int referenceLane,
        out int referenceIndex)
    {
        ulong addressWord = ComputeAddressWord(previous, pass, blockIndex);

        if (memory.Parallelism == 1)
        {
            referenceLane = 0;
        }
        else if (blockIndex % 32 == 0)
        {
            referenceLane = FastRange.Map(previous[1], memory.Parallelism);
        }
        else
        {
            referenceLane = currentLane;
        }

        int allowedCount = GetAllowedBlockCount(memory, pass, slice, currentLane, referenceLane, blockIndex);

        // Cross-lane references in early slices of pass zero may have no completed
        // foreign memory yet. Fall back to the current lane so addressing never
        // touches uninitialized blocks.
        if (allowedCount == 0)
        {
            referenceLane = currentLane;
            allowedCount = GetAllowedBlockCount(memory, pass, slice, currentLane, referenceLane, blockIndex);
        }

        if (allowedCount <= 0)
        {
            throw new CryptographicException("Reference selection produced an empty allowed region.");
        }

        int localIndex = FastRange.Map(addressWord, allowedCount);
        referenceIndex = MapLocalIndexToBlock(memory, pass, slice, currentLane, referenceLane, blockIndex, localIndex);
    }

    private static ulong ComputeAddressWord(ReadOnlySpan<ulong> previous, int pass, int blockIndex)
    {
        unchecked
        {
            return previous[0]
                ^ RotateLeft64(previous[17], 13)
                ^ previous[73]
                ^ (ulong)pass
                ^ RotateLeft64((ulong)blockIndex, 29);
        }
    }

    private static int GetAllowedBlockCount(
        MemoryMatrix memory,
        int pass,
        int slice,
        int currentLane,
        int referenceLane,
        int blockIndex)
    {
        if (referenceLane == currentLane)
        {
            if (pass == 0)
            {
                // All blocks before the current block in this lane.
                return blockIndex;
            }

            // Later passes may use the entire lane (current contents).
            return memory.BlocksPerLane;
        }

        // Cross-lane: only completed slices are visible.
        return slice * memory.SliceLength;
    }

    private static int MapLocalIndexToBlock(
        MemoryMatrix memory,
        int pass,
        int slice,
        int currentLane,
        int referenceLane,
        int blockIndex,
        int localIndex)
    {
        // Allowed regions are always contiguous prefixes starting at block 0.
        _ = memory;
        _ = pass;
        _ = slice;
        _ = currentLane;
        _ = referenceLane;
        _ = blockIndex;
        return localIndex;
    }

    private static byte[] Finalize(MemoryMatrix memory, ReadOnlySpan<byte> seed, ForgeHashParameters parameters)
    {
        byte[] groupRoot = ComputeGroupRoot(memory);
        try
        {
            return FinalizeFromParts(memory, seed, parameters, groupRoot);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(groupRoot);
        }
    }

    private static byte[] FinalizeFromParts(
        MemoryMatrix memory,
        ReadOnlySpan<byte> seed,
        ForgeHashParameters parameters,
        ReadOnlySpan<byte> groupRoot)
    {
        Span<ulong> accumulatorWords = stackalloc ulong[ForgeMix.WordsPerBlock];
        accumulatorWords.Clear();

        int last = memory.BlocksPerLane - 1;
        int quarter = memory.BlocksPerLane / 4;
        int half = memory.BlocksPerLane / 2;
        int threeQuarter = (memory.BlocksPerLane * 3) / 4;

        for (int lane = 0; lane < memory.Parallelism; lane++)
        {
            XorInto(accumulatorWords, memory.GetBlockReadOnly(lane, last));
            XorInto(accumulatorWords, memory.GetBlockReadOnly(lane, quarter));
            XorInto(accumulatorWords, memory.GetBlockReadOnly(lane, half));
            XorInto(accumulatorWords, memory.GetBlockReadOnly(lane, threeQuarter));
        }

        Span<byte> accumulator = stackalloc byte[ForgeMix.BlockSize];
        ForgeMix.WordsToBytes(accumulatorWords, accumulator);

        byte[] prefix = BinaryEncoding.Utf8(Blake3Adapter.FinalPrefix);
        checked
        {
            int rootInputLength =
                prefix.Length +
                seed.Length +
                accumulator.Length +
                groupRoot.Length +
                16;

            byte[] rootInput = new byte[rootInputLength];
            int offset = 0;
            prefix.AsSpan().CopyTo(rootInput);
            offset += prefix.Length;
            seed.CopyTo(rootInput.AsSpan(offset));
            offset += seed.Length;
            accumulator.CopyTo(rootInput.AsSpan(offset));
            offset += accumulator.Length;
            groupRoot.CopyTo(rootInput.AsSpan(offset));
            offset += groupRoot.Length;
            BinaryEncoding.WriteInt32(rootInput, offset, parameters.MemoryKiB);
            offset += 4;
            BinaryEncoding.WriteInt32(rootInput, offset, parameters.Iterations);
            offset += 4;
            BinaryEncoding.WriteInt32(rootInput, offset, parameters.Parallelism);
            offset += 4;
            BinaryEncoding.WriteInt32(rootInput, offset, parameters.OutputLength);

            byte[] root = Blake3Adapter.Hash(rootInput);

            byte[] outputPrefix = BinaryEncoding.Utf8(Blake3Adapter.OutputPrefix);
            byte[] outputInput = new byte[outputPrefix.Length + root.Length + seed.Length];
            outputPrefix.AsSpan().CopyTo(outputInput);
            root.AsSpan().CopyTo(outputInput.AsSpan(outputPrefix.Length));
            seed.CopyTo(outputInput.AsSpan(outputPrefix.Length + root.Length));

            try
            {
                return Blake3Adapter.Xof(outputInput, parameters.OutputLength);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(rootInput);
                CryptographicOperations.ZeroMemory(root);
                CryptographicOperations.ZeroMemory(outputInput);
                CryptographicOperations.ZeroMemory(outputPrefix);
                CryptographicOperations.ZeroMemory(prefix);
                CryptographicOperations.ZeroMemory(accumulator);
                CryptographicOperations.ZeroMemory(
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(accumulatorWords));
            }
        }
    }

    private static byte[] ComputeGroupRoot(MemoryMatrix memory)
    {
        using Hasher groupRootHasher = Hasher.New();
        groupRootHasher.Update(BinaryEncoding.Utf8(Blake3Adapter.GroupRootPrefix));

        const int groupSize = 64;
        int totalBlocks = memory.BlockCount;
        int groupCount = (totalBlocks + groupSize - 1) / groupSize;

        Span<byte> groupBuffer = new byte[checked(groupSize * ForgeMix.BlockSize)];
        Span<byte> meta = stackalloc byte[16];
        Span<byte> digest = stackalloc byte[32];
        Span<byte> groupIndexBytes = stackalloc byte[8];

        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            int groupStart = groupIndex * groupSize;
            int groupBlockCount = Math.Min(groupSize, totalBlocks - groupStart);
            int byteLength = checked(groupBlockCount * ForgeMix.BlockSize);
            Span<byte> groupBytes = groupBuffer[..byteLength];

            for (int i = 0; i < groupBlockCount; i++)
            {
                DecodeFlatBlock(memory, groupStart + i, groupBytes.Slice(i * ForgeMix.BlockSize, ForgeMix.BlockSize));
            }

            byte[] groupPrefix = BinaryEncoding.Utf8(Blake3Adapter.GroupPrefix);
            BinaryPrimitives.WriteUInt64LittleEndian(meta, (ulong)groupIndex);
            BinaryPrimitives.WriteUInt64LittleEndian(meta.Slice(8), (ulong)groupBlockCount);

            using (Hasher groupHasher = Hasher.New())
            {
                groupHasher.Update(groupPrefix);
                groupHasher.Update(meta);
                groupHasher.Update(groupBytes);
                groupHasher.Finalize(digest);
            }

            CryptographicOperations.ZeroMemory(groupPrefix);

            BinaryPrimitives.WriteUInt64LittleEndian(groupIndexBytes, (ulong)groupIndex);
            groupRootHasher.Update(groupIndexBytes);
            groupRootHasher.Update(digest);
        }

        Hash groupRoot = groupRootHasher.Finalize();
        CryptographicOperations.ZeroMemory(groupBuffer);
        CryptographicOperations.ZeroMemory(digest);
        return groupRoot.AsSpan().ToArray();
    }

    private static void DecodeFlatBlock(MemoryMatrix memory, int flatIndex, Span<byte> destination)
    {
        int lane = flatIndex / memory.BlocksPerLane;
        int blockIndex = flatIndex % memory.BlocksPerLane;
        ForgeMix.WordsToBytes(memory.GetBlockReadOnly(lane, blockIndex), destination);
    }

    private static void XorInto(Span<ulong> accumulator, ReadOnlySpan<ulong> block)
    {
        unchecked
        {
            for (int i = 0; i < ForgeMix.WordsPerBlock; i++)
            {
                accumulator[i] ^= block[i];
            }
        }
    }

    private static ulong RotateLeft64(ulong value, int bits)
        => (value << bits) | (value >> (64 - bits));
}
