namespace ForgeHash;

/// <summary>
/// Successfully parsed ForgeHash encoded string.
/// </summary>
public sealed class ParsedForgeHash
{
    /// <summary>Creates a parsed hash record.</summary>
    public ParsedForgeHash(
        string algorithmId,
        int version,
        int memoryKiB,
        int iterations,
        int parallelism,
        byte[] salt,
        byte[] hash,
        string canonicalEncoding,
        bool isCanonical)
    {
        AlgorithmId = algorithmId;
        Version = version;
        MemoryKiB = memoryKiB;
        Iterations = iterations;
        Parallelism = parallelism;
        Salt = salt;
        Hash = hash;
        CanonicalEncoding = canonicalEncoding;
        IsCanonical = isCanonical;
    }

    /// <summary>Algorithm identifier (must be <c>forgeh</c>).</summary>
    public string AlgorithmId { get; }

    /// <summary>Algorithm version.</summary>
    public int Version { get; }

    /// <summary>Memory cost in KiB.</summary>
    public int MemoryKiB { get; }

    /// <summary>Iteration (pass) count.</summary>
    public int Iterations { get; }

    /// <summary>Lane count.</summary>
    public int Parallelism { get; }

    /// <summary>Decoded salt bytes.</summary>
    public byte[] Salt { get; }

    /// <summary>Decoded digest bytes.</summary>
    public byte[] Hash { get; }

    /// <summary>Canonical encoding of this hash.</summary>
    public string CanonicalEncoding { get; }

    /// <summary>Whether the original string matched the canonical form exactly.</summary>
    public bool IsCanonical { get; }

    /// <summary>Parameters reconstructed from the encoded hash.</summary>
    public ForgeHashParameters ToParameters() => new()
    {
        MemoryKiB = MemoryKiB,
        Iterations = Iterations,
        Parallelism = Parallelism,
        OutputLength = Hash.Length,
        SaltLength = Salt.Length,
    };
}
