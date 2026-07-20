using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

namespace ForgeHash.X.Tests;

public sealed class ParallelEquivalenceTests
{
    [Fact]
    public void ParallelAndSequential_MatchForMultipleLanes()
    {
        var parameters = new ForgeHashXParameters
        {
            MemoryKiB = 1024,
            Iterations = 1,
            Parallelism = 2,
        };

        byte[] password = "parallel"u8.ToArray();
        byte[] salt = Enumerable.Range(0, 16).Select(i => (byte)(i * 3)).ToArray();

        byte[] sequential = ForgeHashXApi.DeriveHash(password, salt, parameters);
        byte[] parallel = ForgeHashXApi.DeriveHashParallel(password, salt, parameters);

        Assert.Equal(sequential, parallel);
    }
}
