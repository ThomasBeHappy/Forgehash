namespace ForgeHash.Tests;

public class ParameterTests
{
    private static readonly byte[] Salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
    private static readonly byte[] Password = "parameter-test"u8.ToArray();

    [Fact]
    public void ChangingMemory_ChangesOutput()
    {
        // 8192 and 16384 both valid with p=1
        var a = new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1 };
        var b = new ForgeHashParameters { MemoryKiB = 16384, Iterations = 1, Parallelism = 1 };

        Assert.NotEqual(
            ForgeHash.DeriveHash(Password, Salt, a),
            ForgeHash.DeriveHash(Password, Salt, b));
    }

    [Fact]
    public void ChangingIterations_ChangesOutput()
    {
        var a = new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1 };
        var b = new ForgeHashParameters { MemoryKiB = 8192, Iterations = 2, Parallelism = 1 };

        Assert.NotEqual(
            ForgeHash.DeriveHash(Password, Salt, a),
            ForgeHash.DeriveHash(Password, Salt, b));
    }

    [Fact]
    public void ChangingParallelism_ChangesOutput()
    {
        var a = new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1 };
        var b = new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 2 };

        Assert.NotEqual(
            ForgeHash.DeriveHash(Password, Salt, a),
            ForgeHash.DeriveHash(Password, Salt, b));
    }

    [Fact]
    public void ChangingOutputLength_ChangesOutput()
    {
        var a = new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1, OutputLength = 32 };
        var b = new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1, OutputLength = 48 };

        byte[] ha = ForgeHash.DeriveHash(Password, Salt, a);
        byte[] hb = ForgeHash.DeriveHash(Password, Salt, b);

        Assert.Equal(32, ha.Length);
        Assert.Equal(48, hb.Length);
        Assert.False(ha.AsSpan().SequenceEqual(hb.AsSpan(0, 32)));
    }

    [Theory]
    [InlineData(4096, 1, 1)]
    [InlineData(8192, 0, 1)]
    [InlineData(8192, 1, 0)]
    [InlineData(8192, 1, 3)] // 8192 % 3 != 0
    public void InvalidParameters_AreRejected(int memory, int iterations, int parallelism)
    {
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = memory,
            Iterations = iterations,
            Parallelism = parallelism,
        };

        Assert.ThrowsAny<ArgumentException>(() => ForgeHash.DeriveHash(Password, Salt, parameters));
    }
}
