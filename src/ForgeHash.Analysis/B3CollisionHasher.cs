using ForgeHashApi = ForgeHash.ForgeHash;

namespace ForgeHash.Analysis;

/// <summary>ForgeHash-B3 derive adapter for collision campaigns.</summary>
public sealed class B3CollisionHasher : ICollisionHasher
{
    public static B3CollisionHasher Instance { get; } = new();

    public string AlgorithmId => "forgeh";

    public byte[] Derive(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        CollisionCostSnapshot cost,
        bool preferLaneParallel)
    {
        ForgeHashParameters parameters = cost.ToB3();
        if (preferLaneParallel && parameters.Parallelism > 1)
        {
            return ForgeHashApi.DeriveHashParallel(password, salt, parameters);
        }

        return ForgeHashApi.DeriveHash(password, salt, parameters);
    }

    public byte[] ComputeSeed(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        CollisionCostSnapshot cost)
        => ForgeHashApi.ComputeSeed(password, salt, cost.ToB3());

    public CollisionCostSnapshot[] DistinctParameterSets()
        =>
        [
            CollisionCostSnapshot.FromB3(new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1, OutputLength = 32 }),
            CollisionCostSnapshot.FromB3(new ForgeHashParameters { MemoryKiB = 16384, Iterations = 1, Parallelism = 1, OutputLength = 32 }),
            CollisionCostSnapshot.FromB3(new ForgeHashParameters { MemoryKiB = 8192, Iterations = 2, Parallelism = 1, OutputLength = 32 }),
            CollisionCostSnapshot.FromB3(new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 2, OutputLength = 32 }),
            CollisionCostSnapshot.FromB3(new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1, OutputLength = 16 }),
            CollisionCostSnapshot.FromB3(new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1, OutputLength = 48 }),
        ];

    public CollisionCostSnapshot WithOutputLength(CollisionCostSnapshot cost, int outputLength)
        => cost with { OutputLength = outputLength };
}
