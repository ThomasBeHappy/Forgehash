namespace ForgeHash.Tests;

public class MemoryMutationTests
{
    private static readonly ForgeHashParameters Parameters = ForgeHashParameters.Development;
    private static readonly byte[] Password = "mutate-me"u8.ToArray();
    private static readonly byte[] Salt = Enumerable.Range(0, 16).Select(i => (byte)(i + 7)).ToArray();

    [Theory]
    [InlineData(0, 0)] // beginning
    [InlineData(0, 2048)] // first quarter
    [InlineData(0, 4096)] // middle
    [InlineData(0, 6144)] // third quarter
    [InlineData(0, 8191)] // final block
    public void MutatingSingleBlock_BeforeFinalization_ChangesHash(int lane, int blockIndex)
    {
        byte[] baseline = ForgeHash.DeriveHash(Password, Salt, Parameters);

        byte[] mutated = ForgeHashTestVectors.DeriveHashWithPreFinalMutation(
            Password,
            Salt,
            Parameters,
            (l, b, block) =>
            {
                if (l == lane && b == blockIndex)
                {
                    unchecked
                    {
                        block[0] ^= 0x1;
                    }
                }
            });

        Assert.NotEqual(baseline, mutated);
    }

    [Fact]
    public void MutatingAnotherLane_ChangesHash()
    {
        var multi = new ForgeHashParameters
        {
            MemoryKiB = 8192,
            Iterations = 1,
            Parallelism = 2,
        };

        byte[] baseline = ForgeHash.DeriveHash(Password, Salt, multi);
        byte[] mutated = ForgeHashTestVectors.DeriveHashWithPreFinalMutation(
            Password,
            Salt,
            multi,
            (lane, blockIndex, block) =>
            {
                if (lane == 1 && blockIndex == 100)
                {
                    unchecked
                    {
                        block[17] ^= 0xFFFFFFFFFFFFFFFFUL;
                    }
                }
            });

        Assert.NotEqual(baseline, mutated);
    }
}
