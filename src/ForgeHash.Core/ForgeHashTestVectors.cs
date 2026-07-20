using System.Security.Cryptography;
using ForgeHash.Internal;

namespace ForgeHash;

/// <summary>
/// Research/test-vector generation helpers. Not used by production hashing paths.
/// </summary>
public static class ForgeHashTestVectors
{
    /// <summary>
    /// Generates a detailed intermediate snapshot for official test-vector publication.
    /// </summary>
    public static TestVectorSnapshot Generate(
        string name,
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashParameters parameters)
    {
        ParameterValidator.ValidatePasswordLength(password.Length);
        ParameterValidator.ValidateForHashing(parameters);

        if (salt.Length < ForgeHashParameters.MinimumSaltLength ||
            salt.Length > ForgeHashParameters.MaximumSaltLength)
        {
            throw new ArgumentOutOfRangeException(nameof(salt));
        }

        byte[] passwordCopy = password.ToArray();
        byte[] saltCopy = salt.ToArray();

        byte[] encodedInput = BinaryEncoding.BuildEncodedInput(
            ForgeHashEngine.AlgorithmVersion,
            parameters.MemoryKiB,
            parameters.Iterations,
            parameters.Parallelism,
            parameters.OutputLength,
            passwordCopy,
            saltCopy,
            ReadOnlySpan<byte>.Empty);

        byte[] seed;
        try
        {
            seed = Blake3Adapter.DeriveSeed(encodedInput);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encodedInput);
        }

        using MemoryMatrix memory = new(parameters.MemoryKiB, parameters.Parallelism);
        ForgeHashEngine.InitializeMemoryPublic(memory, seed);

        var initialized = new List<SampledBlock>();
        for (int lane = 0; lane < memory.Parallelism; lane++)
        {
            initialized.Add(Sample(memory, pass: -1, lane, 0));
            initialized.Add(Sample(memory, pass: -1, lane, 1));
        }

        var sampleRefs = new List<BlockReferenceTrace>();
        var mixSamples = new List<SampledBlock>();
        HashSet<string> wantedRefKeys = BuildWantedReferenceKeys(parameters, memory);
        HashSet<string> wantedMixKeys = BuildWantedMixKeys(parameters, memory);

        ForgeHashEngine.FillMemorySequentialPublic(
            memory,
            parameters.Iterations,
            trace =>
            {
                string key = $"{trace.Pass}:{trace.Lane}:{trace.BlockIndex}";
                if (wantedRefKeys.Remove(key))
                {
                    sampleRefs.Add(trace);
                }
            },
            (pass, lane, blockIndex, mixOutput) =>
            {
                string key = $"{pass}:{lane}:{blockIndex}";
                if (!wantedMixKeys.Remove(key))
                {
                    return;
                }

                Span<byte> bytes = stackalloc byte[ForgeMix.BlockSize];
                ForgeMix.WordsToBytes(mixOutput, bytes);
                mixSamples.Add(new SampledBlock(pass, lane, blockIndex, bytes[..32].ToArray()));
            });

        mixSamples.Sort(static (a, b) =>
        {
            int c = a.Pass.CompareTo(b.Pass);
            if (c != 0)
            {
                return c;
            }

            c = a.Lane.CompareTo(b.Lane);
            if (c != 0)
            {
                return c;
            }

            return a.BlockIndex.CompareTo(b.BlockIndex);
        });

        byte[] hash = ForgeHashEngine.FinalizePublic(memory, seed, parameters, out byte[] groupRoot);

        string encoded = ForgeHashEncoding.Encode(
            ForgeHashEngine.AlgorithmVersion,
            parameters.MemoryKiB,
            parameters.Iterations,
            parameters.Parallelism,
            saltCopy,
            hash);

        sampleRefs.Sort(static (a, b) =>
        {
            int c = a.Pass.CompareTo(b.Pass);
            if (c != 0)
            {
                return c;
            }

            c = a.Lane.CompareTo(b.Lane);
            if (c != 0)
            {
                return c;
            }

            return a.BlockIndex.CompareTo(b.BlockIndex);
        });

        return new TestVectorSnapshot
        {
            Name = name,
            Password = passwordCopy,
            Salt = saltCopy,
            Parameters = parameters,
            Seed = seed,
            InitializedBlocks = initialized,
            SampleReferences = sampleRefs,
            ForgeMixSamples = mixSamples,
            GroupRoot = groupRoot,
            Hash = hash,
            Encoded = encoded,
        };
    }

    /// <summary>
    /// Derives a hash after optionally mutating memory immediately before finalization.
    /// Used only for analysis tests proving complete-memory influence.
    /// </summary>
    public static byte[] DeriveHashWithPreFinalMutation(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashParameters parameters,
        Action<int /*lane*/, int /*blockIndex*/, Span<ulong> /*block*/> mutator)
    {
        ParameterValidator.ValidatePasswordLength(password.Length);
        ParameterValidator.ValidateForHashing(parameters);

        byte[] encodedInput = BinaryEncoding.BuildEncodedInput(
            ForgeHashEngine.AlgorithmVersion,
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
            ForgeHashEngine.InitializeMemoryPublic(memory, seed);
            ForgeHashEngine.FillMemorySequentialPublic(memory, parameters.Iterations, observer: null);

            for (int lane = 0; lane < memory.Parallelism; lane++)
            {
                for (int blockIndex = 0; blockIndex < memory.BlocksPerLane; blockIndex++)
                {
                    mutator(lane, blockIndex, memory.GetBlock(lane, blockIndex));
                }
            }

            return ForgeHashEngine.FinalizePublic(memory, seed, parameters, out _);
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

    private static SampledBlock Sample(MemoryMatrix memory, int pass, int lane, int blockIndex)
    {
        Span<byte> bytes = stackalloc byte[ForgeMix.BlockSize];
        ForgeMix.WordsToBytes(memory.GetBlockReadOnly(lane, blockIndex), bytes);
        return new SampledBlock(pass, lane, blockIndex, bytes[..32].ToArray());
    }

    private static HashSet<string> BuildWantedReferenceKeys(ForgeHashParameters parameters, MemoryMatrix memory)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int last = memory.BlocksPerLane - 1;
        int mid = memory.BlocksPerLane / 2;

        for (int pass = 0; pass < parameters.Iterations; pass++)
        {
            for (int lane = 0; lane < parameters.Parallelism; lane++)
            {
                if (pass == 0)
                {
                    keys.Add($"{pass}:{lane}:2");
                }
                else
                {
                    keys.Add($"{pass}:{lane}:0");
                }

                keys.Add($"{pass}:{lane}:32");
                keys.Add($"{pass}:{lane}:{mid}");
                keys.Add($"{pass}:{lane}:{last}");
            }
        }

        return keys;
    }

    private static HashSet<string> BuildWantedMixKeys(ForgeHashParameters parameters, MemoryMatrix memory)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int last = memory.BlocksPerLane - 1;
        int quarter = memory.BlocksPerLane / 4;

        for (int lane = 0; lane < Math.Min(parameters.Parallelism, 2); lane++)
        {
            keys.Add($"0:{lane}:2");
            keys.Add($"0:{lane}:{quarter}");
            keys.Add($"{parameters.Iterations - 1}:{lane}:{last}");
        }

        return keys;
    }
}
