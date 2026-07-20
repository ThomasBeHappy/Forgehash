namespace ForgeHash;

/// <summary>
/// Validates ForgeHash parameters before any memory allocation or expensive work.
/// </summary>
public static class ParameterValidator
{
    /// <summary>
    /// Application policy limits applied by the reference implementation.
    /// </summary>
    public sealed record Limits
    {
        /// <summary>Default defensive limits matching the specification recommendations.</summary>
        public static Limits Default { get; } = new();

        /// <summary>Minimum memory in KiB.</summary>
        public int MinimumMemoryKiB { get; init; } = ForgeHashParameters.MinimumMemoryKiB;

        /// <summary>Maximum memory in KiB.</summary>
        public int MaximumMemoryKiB { get; init; } = ForgeHashParameters.RecommendedMaximumMemoryKiB;

        /// <summary>Minimum iterations.</summary>
        public int MinimumIterations { get; init; } = ForgeHashParameters.MinimumIterations;

        /// <summary>Maximum iterations.</summary>
        public int MaximumIterations { get; init; } = ForgeHashParameters.RecommendedMaximumIterations;

        /// <summary>Minimum parallelism.</summary>
        public int MinimumParallelism { get; init; } = ForgeHashParameters.MinimumParallelism;

        /// <summary>Maximum parallelism.</summary>
        public int MaximumParallelism { get; init; } = 64;

        /// <summary>Whether encoded v1 hashes must use a 32-byte output.</summary>
        public bool RequireEncodedOutputLength32 { get; init; } = true;
    }

    /// <summary>
    /// Validates caller-supplied hashing parameters.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is outside policy.</exception>
    public static void ValidateForHashing(ForgeHashParameters parameters, Limits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        limits ??= Limits.Default;

        ValidateCore(
            parameters.MemoryKiB,
            parameters.Iterations,
            parameters.Parallelism,
            parameters.OutputLength,
            limits,
            forEncodedHash: false);

        if (parameters.SaltLength < ForgeHashParameters.MinimumSaltLength ||
            parameters.SaltLength > ForgeHashParameters.MaximumSaltLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameters),
                $"Salt length must be between {ForgeHashParameters.MinimumSaltLength} and {ForgeHashParameters.MaximumSaltLength} bytes.");
        }
    }

    /// <summary>
    /// Validates parameters decoded from an encoded hash string before allocation.
    /// </summary>
    /// <exception cref="FormatException">Thrown when stored parameters violate policy.</exception>
    public static void ValidateForVerification(
        int memoryKiB,
        int iterations,
        int parallelism,
        int outputLength,
        int saltLength,
        Limits? limits = null)
    {
        limits ??= Limits.Default;

        try
        {
            ValidateCore(memoryKiB, iterations, parallelism, outputLength, limits, forEncodedHash: true);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new FormatException(ex.Message, ex);
        }

        if (saltLength < ForgeHashParameters.MinimumSaltLength ||
            saltLength > ForgeHashParameters.MaximumSaltLength)
        {
            throw new FormatException(
                $"Salt length must be between {ForgeHashParameters.MinimumSaltLength} and {ForgeHashParameters.MaximumSaltLength} bytes.");
        }
    }

    /// <summary>
    /// Validates password length. Passwords must not be silently truncated.
    /// </summary>
    public static void ValidatePasswordLength(int passwordLength)
    {
        if (passwordLength < 0 || passwordLength > ForgeHashParameters.MaximumPasswordLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(passwordLength),
                $"Password length must be between 0 and {ForgeHashParameters.MaximumPasswordLength} bytes.");
        }
    }

    private static void ValidateCore(
        int memoryKiB,
        int iterations,
        int parallelism,
        int outputLength,
        Limits limits,
        bool forEncodedHash)
    {
        if (memoryKiB < limits.MinimumMemoryKiB || memoryKiB > limits.MaximumMemoryKiB)
        {
            throw new ArgumentOutOfRangeException(
                nameof(memoryKiB),
                $"Memory must be between {limits.MinimumMemoryKiB} and {limits.MaximumMemoryKiB} KiB.");
        }

        if (iterations < limits.MinimumIterations || iterations > limits.MaximumIterations)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iterations),
                $"Iterations must be between {limits.MinimumIterations} and {limits.MaximumIterations}.");
        }

        if (parallelism < limits.MinimumParallelism ||
            parallelism > limits.MaximumParallelism ||
            parallelism > ForgeHashParameters.AbsoluteMaximumParallelism)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parallelism),
                $"Parallelism must be between {limits.MinimumParallelism} and {Math.Min(limits.MaximumParallelism, ForgeHashParameters.AbsoluteMaximumParallelism)}.");
        }

        // Algorithm structural constraints — reject before allocation.
        if (memoryKiB < checked(parallelism * 8))
        {
            throw new ArgumentOutOfRangeException(
                nameof(memoryKiB),
                "Memory must provide at least eight blocks per lane.");
        }

        if (memoryKiB % parallelism != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(memoryKiB),
                "Memory block count must be evenly divisible by parallelism.");
        }

        int blocksPerLane = memoryKiB / parallelism;
        if (blocksPerLane % 4 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(memoryKiB),
                "Blocks per lane must be divisible into four equal slices.");
        }

        if (forEncodedHash && limits.RequireEncodedOutputLength32)
        {
            if (outputLength != ForgeHashParameters.DefaultOutputLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outputLength),
                    "Encoded ForgeHash version 1 requires a 32-byte output.");
            }
        }
        else if (outputLength < ForgeHashParameters.MinimumOutputLength ||
                 outputLength > ForgeHashParameters.MaximumOutputLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputLength),
                $"Output length must be between {ForgeHashParameters.MinimumOutputLength} and {ForgeHashParameters.MaximumOutputLength} bytes.");
        }
    }
}
