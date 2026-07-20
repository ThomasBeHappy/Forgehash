namespace ForgeHash;

/// <summary>
/// Configurable cost and output parameters for ForgeHash-B3.
/// </summary>
/// <remarks>
/// ForgeHash is experimental cryptographic software and must not be used to protect
/// production credentials.
/// </remarks>
public sealed record ForgeHashParameters
{
    /// <summary>Recommended interactive default: 65 536 KiB (64 MiB).</summary>
    public const int DefaultMemoryKiB = 65_536;

    /// <summary>Recommended interactive default: 3 passes.</summary>
    public const int DefaultIterations = 3;

    /// <summary>Recommended interactive default: 1 lane.</summary>
    public const int DefaultParallelism = 1;

    /// <summary>Version 1 encoded output length.</summary>
    public const int DefaultOutputLength = 32;

    /// <summary>Recommended salt length.</summary>
    public const int DefaultSaltLength = 16;

    /// <summary>Algorithm minimum memory cost in KiB.</summary>
    public const int MinimumMemoryKiB = 8_192;

    /// <summary>Recommended application maximum memory cost in KiB.</summary>
    public const int RecommendedMaximumMemoryKiB = 1_048_576;

    /// <summary>Minimum iteration count.</summary>
    public const int MinimumIterations = 1;

    /// <summary>Recommended application maximum iterations.</summary>
    public const int RecommendedMaximumIterations = 20;

    /// <summary>Minimum lane count.</summary>
    public const int MinimumParallelism = 1;

    /// <summary>Absolute implementation maximum lane count.</summary>
    public const int AbsoluteMaximumParallelism = 255;

    /// <summary>Minimum salt length in bytes.</summary>
    public const int MinimumSaltLength = 16;

    /// <summary>Maximum salt length in bytes.</summary>
    public const int MaximumSaltLength = 64;

    /// <summary>Minimum research output length in bytes.</summary>
    public const int MinimumOutputLength = 16;

    /// <summary>Maximum research output length in bytes.</summary>
    public const int MaximumOutputLength = 64;

    /// <summary>Defensive maximum password length accepted by the core.</summary>
    public const int MaximumPasswordLength = 1_048_576;

    /// <summary>Memory usage in kibibytes. Each KiB is one 1024-byte block.</summary>
    public int MemoryKiB { get; init; } = DefaultMemoryKiB;

    /// <summary>Number of complete memory passes.</summary>
    public int Iterations { get; init; } = DefaultIterations;

    /// <summary>Number of lanes.</summary>
    public int Parallelism { get; init; } = DefaultParallelism;

    /// <summary>Output digest length in bytes.</summary>
    public int OutputLength { get; init; } = DefaultOutputLength;

    /// <summary>Salt length used when generating a new hash.</summary>
    public int SaltLength { get; init; } = DefaultSaltLength;

    /// <summary>Interactive login profile (64 MiB, 3 passes, 1 lane).</summary>
    public static ForgeHashParameters Interactive { get; } = new();

    /// <summary>Higher-cost profile for sensitive accounts.</summary>
    public static ForgeHashParameters Sensitive { get; } = new()
    {
        MemoryKiB = 262_144,
        Iterations = 4,
        Parallelism = 2,
    };

    /// <summary>
    /// Low-cost profile for local development and tests only.
    /// Must never be silently selected in production builds.
    /// </summary>
    public static ForgeHashParameters Development { get; } = new()
    {
        MemoryKiB = MinimumMemoryKiB,
        Iterations = 1,
        Parallelism = 1,
    };
}
