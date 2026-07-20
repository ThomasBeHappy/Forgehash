namespace ForgeHash;

/// <summary>
/// Captured intermediate values for an official ForgeHash-B3 test vector.
/// </summary>
public sealed class TestVectorSnapshot
{
    /// <summary>Human-readable vector name.</summary>
    public required string Name { get; init; }

    /// <summary>Password input bytes.</summary>
    public required byte[] Password { get; init; }

    /// <summary>Salt input bytes.</summary>
    public required byte[] Salt { get; init; }

    /// <summary>Parameters used for the derivation.</summary>
    public required ForgeHashParameters Parameters { get; init; }

    /// <summary>32-byte BLAKE3 derive-key seed.</summary>
    public required byte[] Seed { get; init; }

    /// <summary>Selected initialized blocks after Expand (lane, index, first 32 bytes).</summary>
    public required IReadOnlyList<SampledBlock> InitializedBlocks { get; init; }

    /// <summary>Selected reference decisions during filling.</summary>
    public required IReadOnlyList<BlockReferenceTrace> SampleReferences { get; init; }

    /// <summary>Selected ForgeMix output samples (first 32 bytes of the block).</summary>
    public required IReadOnlyList<SampledBlock> ForgeMixSamples { get; init; }

    /// <summary>32-byte group-root digest.</summary>
    public required byte[] GroupRoot { get; init; }

    /// <summary>Final password hash output.</summary>
    public required byte[] Hash { get; init; }

    /// <summary>Canonical encoded representation.</summary>
    public required string Encoded { get; init; }
}

/// <summary>
/// A sampled memory block identified by lane and block index.
/// </summary>
/// <param name="Pass">Pass index when sampled, or -1 for initialization.</param>
/// <param name="Lane">Lane index.</param>
/// <param name="BlockIndex">Block index within the lane.</param>
/// <param name="Prefix">First 32 bytes of the little-endian block serialization.</param>
public sealed record SampledBlock(int Pass, int Lane, int BlockIndex, byte[] Prefix);
