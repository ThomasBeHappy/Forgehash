namespace ForgeHash.Analysis;

/// <summary>
/// Pluggable derive/seed surface for empirical collision campaigns (B3 or X).
/// </summary>
public interface ICollisionHasher
{
    string AlgorithmId { get; }

    byte[] Derive(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        CollisionCostSnapshot cost,
        bool preferLaneParallel);

    byte[] ComputeSeed(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        CollisionCostSnapshot cost);

    CollisionCostSnapshot[] DistinctParameterSets();

    CollisionCostSnapshot WithOutputLength(CollisionCostSnapshot cost, int outputLength);
}
