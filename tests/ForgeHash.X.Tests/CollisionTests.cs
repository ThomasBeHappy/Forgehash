using ForgeHash.Analysis;
using ForgeHashX;

namespace ForgeHash.X.Tests;

/// <summary>
/// Empirical uniqueness smoke hunts for ForgeHash-X via shared <see cref="CollisionCampaign"/>.
/// Not a cryptographic proof.
/// </summary>
public sealed class CollisionTests
{
    private static readonly CollisionCostSnapshot Toy =
        CollisionCostSnapshot.FromX(ForgeHashXParameters.Toy);

    [Fact]
    public void DistinctPasswords_SameSalt_ProduceDistinctHashesAndSeeds()
    {
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.DistinctPasswords,
            sampleCount: 48,
            Toy,
            XCollisionHasher.Instance);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal("forgehx", result.Cost.Algorithm);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
        Assert.Equal(48, result.CompletedSamples);
    }

    [Fact]
    public void DistinctSalts_SamePassword_ProduceDistinctHashesAndSeeds()
    {
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.DistinctSalts,
            sampleCount: 48,
            Toy,
            XCollisionHasher.Instance,
            rngSeed: 0xC01115);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
        Assert.Equal(48, result.CompletedSamples);
    }

    [Fact]
    public void NearbyPasswordBitFlips_DoNotCollide()
    {
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.NearbyPasswordBitFlips,
            sampleCount: 48,
            Toy,
            XCollisionHasher.Instance);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
        Assert.Equal(48, result.CompletedSamples);
    }

    [Fact]
    public void DistinctParameterSets_SamePasswordSalt_ProduceDistinctHashes()
    {
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.DistinctParameterSets,
            sampleCount: 6,
            Toy,
            XCollisionHasher.Instance);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal(6, result.CompletedSamples);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
    }

    [Fact]
    public void RandomCampaign_NoFinalHashCollisions()
    {
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.RandomPairs,
            sampleCount: 48,
            Toy,
            XCollisionHasher.Instance,
            rngSeed: 0x5A17);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal(48, result.CompletedSamples);
        Assert.Equal(48, result.UniqueDigests);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
    }

    [Fact]
    public void TruncatedOutputs_StillSeparateNearbyInputs()
    {
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.TruncatedOutputs,
            sampleCount: 48,
            Toy,
            XCollisionHasher.Instance);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal(16, result.Cost.OutputLength);
        Assert.Equal(48, result.CompletedSamples);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
    }
}
