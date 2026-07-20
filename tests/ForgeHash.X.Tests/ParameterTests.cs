using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

namespace ForgeHash.X.Tests;

public sealed class ParameterTests
{
    private static readonly byte[] Salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
    private static readonly byte[] Password = "parameter-test"u8.ToArray();

    [Fact]
    public void ChangingMemory_ChangesOutput()
    {
        var a = new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 1 };
        var b = new ForgeHashXParameters { MemoryKiB = 2048, Iterations = 1, Parallelism = 1 };

        Assert.NotEqual(
            ForgeHashXApi.DeriveHash(Password, Salt, a),
            ForgeHashXApi.DeriveHash(Password, Salt, b));
    }

    [Fact]
    public void ChangingIterations_ChangesOutput()
    {
        var a = new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 1 };
        var b = new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 2, Parallelism = 1 };

        Assert.NotEqual(
            ForgeHashXApi.DeriveHash(Password, Salt, a),
            ForgeHashXApi.DeriveHash(Password, Salt, b));
    }

    [Fact]
    public void ChangingParallelism_ChangesOutput()
    {
        var a = new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 1 };
        var b = new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 2 };

        Assert.NotEqual(
            ForgeHashXApi.DeriveHash(Password, Salt, a),
            ForgeHashXApi.DeriveHash(Password, Salt, b));
    }

    [Fact]
    public void ChangingOutputLength_ChangesOutput()
    {
        var a = new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 1, OutputLength = 32 };
        var b = new ForgeHashXParameters { MemoryKiB = 1024, Iterations = 1, Parallelism = 1, OutputLength = 48 };

        byte[] ha = ForgeHashXApi.DeriveHash(Password, Salt, a);
        byte[] hb = ForgeHashXApi.DeriveHash(Password, Salt, b);

        Assert.Equal(32, ha.Length);
        Assert.Equal(48, hb.Length);
        Assert.False(ha.AsSpan().SequenceEqual(hb.AsSpan(0, 32)));
    }

    [Theory]
    [InlineData(128, 1, 1)]
    [InlineData(1024, 0, 1)]
    [InlineData(1024, 1, 0)]
    [InlineData(1024, 1, 3)] // blockCount % 3 != 0
    public void InvalidParameters_AreRejected(int memory, int iterations, int parallelism)
    {
        var parameters = new ForgeHashXParameters
        {
            MemoryKiB = memory,
            Iterations = iterations,
            Parallelism = parallelism,
        };

        Assert.ThrowsAny<ArgumentException>(() => ForgeHashXApi.DeriveHash(Password, Salt, parameters));
    }
}
