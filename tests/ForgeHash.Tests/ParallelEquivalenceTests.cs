namespace ForgeHash.Tests;

public class ParallelEquivalenceTests
{
    [Fact]
    public void ParallelAndSequential_MatchForMultipleLanes()
    {
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = 8192,
            Iterations = 1,
            Parallelism = 2,
        };

        byte[] password = "parallel"u8.ToArray();
        byte[] salt = Enumerable.Range(0, 16).Select(i => (byte)(i * 3)).ToArray();

        byte[] sequential = ForgeHash.DeriveHash(password, salt, parameters);
        byte[] parallel = ForgeHash.DeriveHashParallel(password, salt, parameters);

        Assert.Equal(sequential, parallel);
    }
}
