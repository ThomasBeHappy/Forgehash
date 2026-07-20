using System.Buffers.Binary;

namespace ForgeHashX;

/// <summary>ForgeHash-X v0 memory-hard engine (SPECIFICATION_X §§6–12).</summary>
internal static class ForgeHashXEngine
{
    public const string TagSeed = "ForgeX/v0/seed";
    public const string TagExpand = "ForgeX/v0/expand";
    public const string TagFinal = "ForgeX/v0/final";
    public const string TagOutput = "ForgeX/v0/output";

    public static byte[] ComputeSeed(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashXParameters parameters)
    {
        parameters.Validate();
        ValidateSalt(salt);
        return ForgeX.Hash(TagSeed, BuildMaterial(password, salt, parameters));
    }

    public static byte[] DeriveHash(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashXParameters parameters,
        bool useParallelLanes = false)
    {
        parameters.Validate();
        ValidateSalt(salt);

        byte[] seed = ForgeX.Hash(TagSeed, BuildMaterial(password, salt, parameters));
        int parallelism = parameters.Parallelism;
        int blocksPerLane = parameters.BlocksPerLane;
        int sliceLength = parameters.SliceLength;

        ulong[] memory = new ulong[parameters.BlockCount * ForgeHashXParameters.WordsPerBlock];
        InitializeMemory(memory, seed, parallelism, blocksPerLane);

        if (useParallelLanes && parallelism > 1)
        {
            FillMemoryParallel(memory, parameters.Iterations, parallelism, blocksPerLane, sliceLength);
        }
        else
        {
            FillMemorySequential(memory, parameters.Iterations, parallelism, blocksPerLane, sliceLength);
        }

        return Finalize(memory, seed, parameters, parallelism, blocksPerLane);
    }

    private static void InitializeMemory(
        ulong[] memory,
        ReadOnlySpan<byte> seed,
        int parallelism,
        int blocksPerLane)
    {
        Span<byte> expandInput = stackalloc byte[32 + 4 + 4];
        seed.CopyTo(expandInput);
        for (int lane = 0; lane < parallelism; lane++)
        {
            for (int i = 0; i < 2; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(expandInput.Slice(32), lane);
                BinaryPrimitives.WriteInt32LittleEndian(expandInput.Slice(36), i);
                byte[] blockBytes = ForgeX.Xof(TagExpand, expandInput, ForgeHashXParameters.BlockSize);
                BinaryUtil.BytesToWords(blockBytes, Block(memory, lane, i, blocksPerLane));
            }
        }
    }

    private static void FillMemorySequential(
        ulong[] memory,
        int iterations,
        int parallelism,
        int blocksPerLane,
        int sliceLength)
    {
        Span<ulong> prev = stackalloc ulong[ForgeHashXParameters.WordsPerBlock];
        Span<ulong> reference = stackalloc ulong[ForgeHashXParameters.WordsPerBlock];
        Span<ulong> combined = stackalloc ulong[ForgeHashXParameters.WordsPerBlock];
        Span<ulong> mixed = stackalloc ulong[ForgeHashXParameters.WordsPerBlock];

        for (int pass = 0; pass < iterations; pass++)
        {
            for (int slice = 0; slice < 4; slice++)
            {
                for (int lane = 0; lane < parallelism; lane++)
                {
                    ProcessLaneSlice(
                        memory, pass, slice, lane, parallelism, blocksPerLane, sliceLength,
                        prev, reference, combined, mixed);
                }
            }
        }
    }

    private static void FillMemoryParallel(
        ulong[] memory,
        int iterations,
        int parallelism,
        int blocksPerLane,
        int sliceLength)
    {
        // Parallel lanes with a barrier after each slice. Workers never write the same lane.
        for (int pass = 0; pass < iterations; pass++)
        {
            for (int slice = 0; slice < 4; slice++)
            {
                int capturedPass = pass;
                int capturedSlice = slice;
                Parallel.For(0, parallelism, lane =>
                {
                    ulong[] prev = new ulong[ForgeHashXParameters.WordsPerBlock];
                    ulong[] reference = new ulong[ForgeHashXParameters.WordsPerBlock];
                    ulong[] combined = new ulong[ForgeHashXParameters.WordsPerBlock];
                    ulong[] mixed = new ulong[ForgeHashXParameters.WordsPerBlock];
                    ProcessLaneSlice(
                        memory, capturedPass, capturedSlice, lane, parallelism, blocksPerLane, sliceLength,
                        prev, reference, combined, mixed);
                });
            }
        }
    }

    private static void ProcessLaneSlice(
        ulong[] memory,
        int pass,
        int slice,
        int lane,
        int parallelism,
        int blocksPerLane,
        int sliceLength,
        Span<ulong> prev,
        Span<ulong> reference,
        Span<ulong> combined,
        Span<ulong> mixed)
    {
        int start = slice * sliceLength;
        int end = start + sliceLength;
        if (pass == 0 && slice == 0)
        {
            start = 2;
        }

        for (int blockIndex = start; blockIndex < end; blockIndex++)
        {
            int previousIndex = blockIndex > 0 ? blockIndex - 1 : blocksPerLane - 1;
            Block(memory, lane, previousIndex, blocksPerLane).CopyTo(prev);

            SelectReference(
                prev,
                pass,
                slice,
                lane,
                blockIndex,
                parallelism,
                blocksPerLane,
                sliceLength,
                out int refLane,
                out int refIndex);

            Block(memory, refLane, refIndex, blocksPerLane).CopyTo(reference);
            for (int w = 0; w < ForgeHashXParameters.WordsPerBlock; w++)
            {
                combined[w] = prev[w] ^ reference[w];
            }

            BlockMix(combined, (ulong)pass, (ulong)lane, (ulong)blockIndex, mixed);

            Span<ulong> cur = Block(memory, lane, blockIndex, blocksPerLane);
            if (pass == 0)
            {
                mixed.CopyTo(cur);
            }
            else
            {
                for (int w = 0; w < ForgeHashXParameters.WordsPerBlock; w++)
                {
                    cur[w] ^= mixed[w];
                }
            }
        }
    }

    private static byte[] Finalize(
        ulong[] memory,
        ReadOnlySpan<byte> seed,
        ForgeHashXParameters parameters,
        int parallelism,
        int blocksPerLane)
    {
        Span<ulong> fold = stackalloc ulong[ForgeHashXParameters.WordsPerBlock];
        fold.Clear();
        int last = blocksPerLane - 1;
        int q1 = blocksPerLane / 4;
        int q2 = blocksPerLane / 2;
        int q3 = (blocksPerLane * 3) / 4;
        for (int lane = 0; lane < parallelism; lane++)
        {
            foreach (int index in new[] { last, q1, q2, q3 })
            {
                ReadOnlySpan<ulong> blk = Block(memory, lane, index, blocksPerLane);
                for (int w = 0; w < ForgeHashXParameters.WordsPerBlock; w++)
                {
                    fold[w] ^= blk[w];
                }
            }
        }

        byte[] foldBytes = new byte[ForgeHashXParameters.BlockSize];
        BinaryUtil.WordsToBytes(fold, foldBytes);

        byte[] finalInput = new byte[32 + ForgeHashXParameters.BlockSize + 16];
        seed.CopyTo(finalInput.AsSpan(0, 32));
        foldBytes.CopyTo(finalInput.AsSpan(32));
        int o = 32 + ForgeHashXParameters.BlockSize;
        BinaryPrimitives.WriteInt32LittleEndian(finalInput.AsSpan(o), parameters.MemoryKiB);
        BinaryPrimitives.WriteInt32LittleEndian(finalInput.AsSpan(o + 4), parameters.Iterations);
        BinaryPrimitives.WriteInt32LittleEndian(finalInput.AsSpan(o + 8), parameters.Parallelism);
        BinaryPrimitives.WriteInt32LittleEndian(finalInput.AsSpan(o + 12), parameters.OutputLength);

        byte[] root = ForgeX.Hash(TagFinal, finalInput);
        byte[] outputInput = new byte[32 + 32];
        root.CopyTo(outputInput.AsSpan(0, 32));
        seed.CopyTo(outputInput.AsSpan(32, 32));
        return ForgeX.Xof(TagOutput, outputInput, parameters.OutputLength);
    }

    private static void BlockMix(
        ReadOnlySpan<ulong> input,
        ulong pass,
        ulong lane,
        ulong blockIndex,
        Span<ulong> output)
    {
        Span<ulong> state = stackalloc ulong[ForgePerm.Words];
        for (int k = 0; k < 4; k++)
        {
            ReadOnlySpan<ulong> chunk = input.Slice(k * ForgePerm.Words, ForgePerm.Words);
            chunk.CopyTo(state);
            unchecked
            {
                state[0] ^= pass;
                state[1] ^= lane;
                state[2] ^= blockIndex;
                state[3] ^= ForgePerm.Rotl(pass + blockIndex + (ulong)k, 13);
            }

            ForgePerm.Permute(state);
            Span<ulong> outChunk = output.Slice(k * ForgePerm.Words, ForgePerm.Words);
            for (int i = 0; i < ForgePerm.Words; i++)
            {
                outChunk[i] = state[i] ^ chunk[i];
            }
        }
    }

    private static void SelectReference(
        ReadOnlySpan<ulong> previous,
        int pass,
        int slice,
        int currentLane,
        int blockIndex,
        int parallelism,
        int blocksPerLane,
        int sliceLength,
        out int referenceLane,
        out int referenceIndex)
    {
        unchecked
        {
            ulong addressWord =
                previous[0]
                ^ ForgePerm.Rotl(previous[9], 19)
                ^ previous[31]
                ^ (ulong)(uint)pass
                ^ ForgePerm.Rotl((ulong)(uint)blockIndex, 11);

            int lane;
            if (parallelism == 1)
            {
                lane = 0;
            }
            else if (blockIndex % 16 == 0)
            {
                lane = (int)BinaryUtil.FastRange(previous[1], (ulong)(uint)parallelism);
            }
            else
            {
                lane = currentLane;
            }

            ulong Allowed(int refLane)
            {
                if (refLane == currentLane)
                {
                    return pass == 0 ? (ulong)(uint)blockIndex : (ulong)(uint)blocksPerLane;
                }

                return (ulong)(uint)(slice * sliceLength);
            }

            ulong allowed = Allowed(lane);
            if (allowed == 0)
            {
                lane = currentLane;
                allowed = Allowed(lane);
            }

            referenceLane = lane;
            referenceIndex = (int)BinaryUtil.FastRange(addressWord, allowed);
        }
    }

    private static Span<ulong> Block(ulong[] memory, int lane, int index, int blocksPerLane)
    {
        int start = (lane * blocksPerLane + index) * ForgeHashXParameters.WordsPerBlock;
        return memory.AsSpan(start, ForgeHashXParameters.WordsPerBlock);
    }

    private static byte[] BuildMaterial(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashXParameters parameters)
    {
        byte[] buf = new byte[4 * 5 + 8 + password.Length + 4 + salt.Length];
        int o = 0;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), 0); o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), parameters.MemoryKiB); o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), parameters.Iterations); o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), parameters.Parallelism); o += 4;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), parameters.OutputLength); o += 4;
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(o), password.Length); o += 8;
        password.CopyTo(buf.AsSpan(o)); o += password.Length;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), salt.Length); o += 4;
        salt.CopyTo(buf.AsSpan(o));
        return buf;
    }

    private static void ValidateSalt(ReadOnlySpan<byte> salt)
    {
        if (salt.Length < ForgeHashXParameters.MinimumSaltLength ||
            salt.Length > ForgeHashXParameters.MaximumSaltLength)
        {
            throw new ArgumentOutOfRangeException(nameof(salt), "Salt length out of range.");
        }
    }
}
