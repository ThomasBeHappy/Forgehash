namespace ForgeHash.Analysis;

/// <summary>
/// Algorithm-agnostic cost parameters for collision / uniqueness campaigns.
/// </summary>
public sealed record CollisionCostSnapshot(
    string Algorithm,
    int MemoryKiB,
    int Iterations,
    int Parallelism,
    int OutputLength,
    int SaltLength)
{
    public string Summary =>
        $"algo={Algorithm},m={MemoryKiB},t={Iterations},p={Parallelism},out={OutputLength}";

    public static CollisionCostSnapshot FromB3(ForgeHashParameters parameters)
        => new(
            "forgeh",
            parameters.MemoryKiB,
            parameters.Iterations,
            parameters.Parallelism,
            parameters.OutputLength,
            parameters.SaltLength);

    public static CollisionCostSnapshot FromX(ForgeHashX.ForgeHashXParameters parameters)
        => new(
            "forgehx",
            parameters.MemoryKiB,
            parameters.Iterations,
            parameters.Parallelism,
            parameters.OutputLength,
            parameters.SaltLength);

    public ForgeHashParameters ToB3()
        => new()
        {
            MemoryKiB = MemoryKiB,
            Iterations = Iterations,
            Parallelism = Parallelism,
            OutputLength = OutputLength,
            SaltLength = SaltLength,
        };

    public ForgeHashX.ForgeHashXParameters ToX()
        => new()
        {
            MemoryKiB = MemoryKiB,
            Iterations = Iterations,
            Parallelism = Parallelism,
            OutputLength = OutputLength,
            SaltLength = SaltLength,
        };
}
