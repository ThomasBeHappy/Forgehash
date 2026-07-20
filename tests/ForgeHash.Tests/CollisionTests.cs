using ForgeHash.Analysis;

namespace ForgeHash.Tests;

/// <summary>
/// Collision / uniqueness campaign for passwords, salts, seeds, and parameters (§32).
/// Empirical smoke hunt via shared <see cref="CollisionCampaign"/> engine — not a cryptographic proof.
/// </summary>
public class CollisionTests
{
    private static readonly ForgeHashParameters Dev = ForgeHashParameters.Development;

    [Fact]
    public void DistinctPasswords_SameSalt_ProduceDistinctHashesAndSeeds()
    {
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.DistinctPasswords,
            sampleCount: 64,
            Dev);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
        Assert.Equal(64, result.CompletedSamples);
    }

    [Fact]
    public void DistinctSalts_SamePassword_ProduceDistinctHashesAndSeeds()
    {
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.DistinctSalts,
            sampleCount: 64,
            Dev,
            rngSeed: 0xC01115);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
        Assert.Equal(64, result.CompletedSamples);
    }

    [Fact]
    public void NearbyPasswordBitFlips_DoNotCollide()
    {
        // 8-byte password ⇒ 64 single-bit neighbors (engine derives length from N).
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.NearbyPasswordBitFlips,
            sampleCount: 64,
            Dev);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
        Assert.Equal(64, result.CompletedSamples);
    }

    [Fact]
    public void DistinctParameterSets_SamePasswordSalt_ProduceDistinctHashes()
    {
        CollisionCampaignResult result = CollisionCampaign.Run(
            CollisionCampaignKind.DistinctParameterSets,
            sampleCount: 6,
            Dev);

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
            Dev,
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
            sampleCount: 64,
            Dev);

        Assert.Equal(0, result.CollisionCount);
        Assert.Equal(16, result.Parameters.OutputLength);
        Assert.Equal(64, result.CompletedSamples);
        Assert.Equal(CollisionStopReason.Completed, result.StopReason);
    }
}
