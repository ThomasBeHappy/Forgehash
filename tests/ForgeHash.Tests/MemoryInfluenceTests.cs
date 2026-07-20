namespace ForgeHash.Tests;

public class MemoryInfluenceTests
{
    [Fact]
    public void References_CoverLaterBlocks_NotOnlyPrefix()
    {
        var parameters = ForgeHashParameters.Development;
        byte[] password = "memory-influence"u8.ToArray();
        byte[] salt = new byte[16];
        var traces = new List<BlockReferenceTrace>();

        _ = ForgeHash.DeriveHashWithTrace(password, salt, parameters, traces.Add);

        Assert.NotEmpty(traces);
        int maxRef = traces.Max(t => t.ReferenceIndex);
        int maxBlock = traces.Max(t => t.BlockIndex);

        // Filling must progress through the lane and reference deep into allocated memory.
        Assert.True(maxBlock > parameters.MemoryKiB / 2);
        Assert.True(maxRef > parameters.MemoryKiB / 4);
    }

    [Fact]
    public void CrossLaneReferences_OccurWithMultipleLanes()
    {
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = 8192,
            Iterations = 1,
            Parallelism = 2,
        };

        var traces = new List<BlockReferenceTrace>();
        _ = ForgeHash.DeriveHashWithTrace("cross"u8, new byte[16], parameters, traces.Add);

        Assert.Contains(traces, t => t.CrossLane);
    }
}
