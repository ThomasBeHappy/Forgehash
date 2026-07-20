namespace ForgeHash.Tests;

public class ReferenceDistributionTests
{
    [Fact]
    public void References_ReachDeepIntoLane_AndAreNotStuckOnPrefix()
    {
        var parameters = ForgeHashParameters.Development;
        var traces = new List<BlockReferenceTrace>();
        _ = ForgeHash.DeriveHashWithTrace("distribution"u8, new byte[16], parameters, traces.Add);

        int blocksPerLane = parameters.MemoryKiB / parameters.Parallelism;
        int[] counts = new int[blocksPerLane];
        foreach (BlockReferenceTrace trace in traces)
        {
            counts[trace.ReferenceIndex]++;
        }

        int referenced = counts.Count(c => c > 0);
        int maxCount = counts.Max();
        double coverage = referenced / (double)blocksPerLane;

        // Pass-zero only allows prefixes, so coverage of the full lane is incomplete by design.
        // Still expect broad use of early/mid regions and no single-block monopoly.
        Assert.True(coverage > 0.20, $"Coverage too low: {coverage:P1}");
        Assert.True(maxCount < traces.Count * 0.05, $"Reference hotspot too strong: {maxCount}/{traces.Count}");
        Assert.True(counts.Take(blocksPerLane / 2).Sum() > 0);
        Assert.Contains(traces, t => t.ReferenceIndex > blocksPerLane / 4);
    }

    [Fact]
    public void MultiLane_ProducesCrossLaneTraffic_AfterFirstSlice()
    {
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = 8192,
            Iterations = 1,
            Parallelism = 2,
        };

        var traces = new List<BlockReferenceTrace>();
        _ = ForgeHash.DeriveHashWithTrace("lanes"u8, new byte[16], parameters, traces.Add);

        List<BlockReferenceTrace> cross = traces.Where(t => t.CrossLane).ToList();
        Assert.NotEmpty(cross);
        Assert.Contains(cross, t => t.Slice > 0);
        Assert.All(cross, t => Assert.True(t.BlockIndex % 32 == 0));
    }

    [Fact]
    public void LaterPasses_CanReferenceEntireCurrentLane()
    {
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = 8192,
            Iterations = 2,
            Parallelism = 1,
        };

        var traces = new List<BlockReferenceTrace>();
        _ = ForgeHash.DeriveHashWithTrace("pass1"u8, new byte[16], parameters, traces.Add);

        int last = (parameters.MemoryKiB / parameters.Parallelism) - 1;
        Assert.Contains(traces, t => t.Pass == 1 && t.ReferenceIndex == last);
    }
}
