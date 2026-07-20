using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

namespace ForgeHash.Analysis;

/// <summary>
/// ForgeHash-X v0 derive adapter for collision campaigns.
/// Experimental — not for production; not B3-compatible.
/// </summary>
public sealed class XCollisionHasher : ICollisionHasher
{
    public static XCollisionHasher Instance { get; } = new();

    public string AlgorithmId => "forgehx";

    public byte[] Derive(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        CollisionCostSnapshot cost,
        bool preferLaneParallel)
    {
        ForgeHashXParameters parameters = cost.ToX();
        if (preferLaneParallel && parameters.Parallelism > 1)
        {
            return ForgeHashXApi.DeriveHashParallel(password, salt, parameters);
        }

        return ForgeHashXApi.DeriveHash(password, salt, parameters);
    }

    public byte[] ComputeSeed(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        CollisionCostSnapshot cost)
        => ForgeHashXApi.ComputeSeed(password, salt, cost.ToX());

    public CollisionCostSnapshot[] DistinctParameterSets()
        =>
        [
            CollisionCostSnapshot.FromX(new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 1, OutputLength = 32 }),
            CollisionCostSnapshot.FromX(new ForgeHashXParameters { MemoryKiB = 2048, Iterations = 1, Parallelism = 1, OutputLength = 32 }),
            CollisionCostSnapshot.FromX(new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 2, Parallelism = 1, OutputLength = 32 }),
            CollisionCostSnapshot.FromX(new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 2, OutputLength = 32 }),
            CollisionCostSnapshot.FromX(new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 1, OutputLength = 16 }),
            CollisionCostSnapshot.FromX(new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 1, OutputLength = 48 }),
        ];

    public CollisionCostSnapshot WithOutputLength(CollisionCostSnapshot cost, int outputLength)
        => cost with { OutputLength = outputLength };
}
