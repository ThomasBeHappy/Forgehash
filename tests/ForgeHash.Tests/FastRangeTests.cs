namespace ForgeHash.Tests;

public class FastRangeTests
{
    [Fact]
    public void Map_StaysWithinRange()
    {
        for (ulong x = 0; x < 10_000; x++)
        {
            int value = FastRange.Map(x * 0x9E3779B97F4A7C15UL, 17);
            Assert.InRange(value, 0, 16);
        }
    }

    [Fact]
    public void Map_RejectsZeroRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FastRange.Map(1UL, 0UL));
    }

    [Fact]
    public void Map_IsDeterministic()
    {
        Assert.Equal(FastRange.Map(123456789UL, 64), FastRange.Map(123456789UL, 64));
    }
}
