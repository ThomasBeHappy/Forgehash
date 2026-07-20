using ForgeHash.Analysis;

namespace ForgeHash.Tests;

public class SecurityAnalysisTests
{
    [Fact]
    public void SparseFinalization_NonSampledBlockStillInfluencesHash()
    {
        // Blocks that are NOT lane-sampled (not at 1/4, 1/2, 3/4, last) must still
        // affect the digest via 64-block group hashing.
        var parameters = ForgeHashParameters.Development;
        byte[] password = "sparse"u8.ToArray();
        byte[] salt = new byte[16];

        byte[] baseline = ForgeHash.DeriveHash(password, salt, parameters);
        byte[] mutated = ForgeHashTestVectors.DeriveHashWithPreFinalMutation(
            password,
            salt,
            parameters,
            (lane, blockIndex, block) =>
            {
                if (lane == 0 && blockIndex == 3)
                {
                    unchecked
                    {
                        block[5] ^= 0xDEADBEEFUL;
                    }
                }
            });

        Assert.NotEqual(baseline, mutated);
    }

    [Fact]
    public void LaneIndependence_CrossLaneReferencesAreMaterial()
    {
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = 8192,
            Iterations = 1,
            Parallelism = 4,
        };

        ReferenceAnalysis analysis = ReferenceAnalysis.Capture("lanes"u8, new byte[16], parameters);
        double rate = analysis.Traces.Count(t => t.CrossLane) / (double)analysis.Traces.Count;

        Assert.True(rate > 0.01, $"Cross-lane rate too low for independence claims: {rate:P2}");
        Assert.NotEmpty(analysis.BuildLaneInteraction());
    }

    [Fact]
    public void ReferencePredictability_BlockIndexAloneIsWeakPredictor()
    {
        var parameters = ForgeHashParameters.Development;
        ReferenceAnalysis analysis = ReferenceAnalysis.Capture("predict"u8, new byte[16], parameters);

        // If references were a trivial function of blockIndex (e.g. blockIndex-1),
        // many traces would satisfy ref == blockIndex - 1.
        int trivialPrev = analysis.Traces.Count(t => t.ReferenceIndex == t.PreviousIndex);
        double trivialRate = trivialPrev / (double)analysis.Traces.Count;
        Assert.True(trivialRate < 0.15, $"References look too predictable from previous index: {trivialRate:P1}");
    }

    [Fact]
    public void TmtoLadder_LowerRetention_IncreasesEstimatedCost()
    {
        ReferenceAnalysis analysis = ReferenceAnalysis.Capture("tmto"u8, new byte[16], ForgeHashParameters.Development);
        IReadOnlyList<TmtoEstimate> ladder = analysis.EstimateStandardTmtoLadder();

        Assert.Equal(1.0, ladder[0].EstimatedExtraMixFactor, precision: 3);
        Assert.True(ladder[^1].RetentionFraction < ladder[1].RetentionFraction);
        Assert.True(ladder[^1].EstimatedExtraMixFactor > ladder[1].EstimatedExtraMixFactor);
        Assert.True(ladder[^1].ReferenceMissRate > 0.5);
    }

    [Fact]
    public void AnalysisExports_AreNonEmpty()
    {
        ReferenceAnalysis analysis = ReferenceAnalysis.Capture("export"u8, new byte[16], ForgeHashParameters.Development);

        Assert.Contains("pass,slice,lane", analysis.ToCsv());
        Assert.Contains("digraph", analysis.ToDependencyGraphDot());
        Assert.Contains("tmto", analysis.ToReportJson());
        Assert.NotEmpty(analysis.BuildHeatMap());
    }
}
