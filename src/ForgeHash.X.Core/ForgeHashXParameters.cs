namespace ForgeHashX;

/// <summary>Sandbox parameters for ForgeHash-X v0.</summary>
public sealed record ForgeHashXParameters
{
    public const int BlockSize = 512;
    public const int WordsPerBlock = BlockSize / sizeof(ulong);
    public const int MinimumMemoryKiB = 256;
    public const int MaximumMemoryKiB = 65_536;
    public const int MinimumIterations = 1;
    public const int MaximumIterations = 8;
    public const int MinimumParallelism = 1;
    public const int MaximumParallelism = 16;
    public const int MinimumOutputLength = 16;
    public const int MaximumOutputLength = 64;
    public const int MinimumSaltLength = 16;
    public const int MaximumSaltLength = 64;
    public const int DefaultOutputLength = 32;

    /// <summary>Toy-vector / sandbox default: 1024 KiB, t=1, p=1.</summary>
    public static ForgeHashXParameters Toy { get; } = new()
    {
        MemoryKiB = 1024,
        Iterations = 1,
        Parallelism = 1,
        OutputLength = DefaultOutputLength,
        SaltLength = 16,
    };

    public int MemoryKiB { get; init; } = 1024;
    public int Iterations { get; init; } = 1;
    public int Parallelism { get; init; } = 1;
    public int OutputLength { get; init; } = DefaultOutputLength;
    public int SaltLength { get; init; } = 16;

    public int BlockCount => MemoryKiB * 1024 / BlockSize;
    public int BlocksPerLane => BlockCount / Parallelism;
    public int SliceLength => BlocksPerLane / 4;

    public void Validate()
    {
        if (MemoryKiB < MinimumMemoryKiB || MemoryKiB > MaximumMemoryKiB)
        {
            throw new ArgumentOutOfRangeException(nameof(MemoryKiB), "memoryKiB out of sandbox range.");
        }

        if (MemoryKiB * 1024 % BlockSize != 0)
        {
            throw new ArgumentException("memoryKiB must yield a whole number of 512-byte blocks.");
        }

        if (Iterations < MinimumIterations || Iterations > MaximumIterations)
        {
            throw new ArgumentOutOfRangeException(nameof(Iterations));
        }

        if (Parallelism < MinimumParallelism || Parallelism > MaximumParallelism)
        {
            throw new ArgumentOutOfRangeException(nameof(Parallelism));
        }

        if (BlockCount % Parallelism != 0)
        {
            throw new ArgumentException("blockCount must be divisible by parallelism.");
        }

        if (BlocksPerLane % 4 != 0)
        {
            throw new ArgumentException("blocksPerLane must be divisible by 4.");
        }

        if (BlocksPerLane < 8)
        {
            throw new ArgumentException("blocksPerLane must be at least 8.");
        }

        if (OutputLength < MinimumOutputLength || OutputLength > MaximumOutputLength)
        {
            throw new ArgumentOutOfRangeException(nameof(OutputLength));
        }

        if (SaltLength < MinimumSaltLength || SaltLength > MaximumSaltLength)
        {
            throw new ArgumentOutOfRangeException(nameof(SaltLength));
        }
    }
}
