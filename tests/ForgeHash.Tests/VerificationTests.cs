namespace ForgeHash.Tests;

public class VerificationTests
{
    [Fact]
    public void Verify_SucceedsForCorrectPassword()
    {
        string encoded = ForgeHash.HashPassword("correct horse", ForgeHashParameters.Development);
        Assert.True(ForgeHash.VerifyPassword("correct horse", encoded));
    }

    [Fact]
    public void Verify_FailsForWrongPassword()
    {
        string encoded = ForgeHash.HashPassword("correct horse", ForgeHashParameters.Development);
        Assert.False(ForgeHash.VerifyPassword("wrong battery", encoded));
    }

    [Fact]
    public void Verify_ReturnsFalseForMalformedHash()
    {
        Assert.False(ForgeHash.VerifyPassword("password", "not-a-hash"));
    }

    [Fact]
    public void NeedsRehash_WhenMemoryTooLow()
    {
        string encoded = ForgeHash.HashPassword("x", ForgeHashParameters.Development);
        var desired = new ForgeHashParameters { MemoryKiB = 16384, Iterations = 1, Parallelism = 1 };
        Assert.True(ForgeHash.NeedsRehash(encoded, desired));
    }

    [Fact]
    public void NeedsRehash_FalseWhenStoredStronger()
    {
        var strong = new ForgeHashParameters { MemoryKiB = 16384, Iterations = 2, Parallelism = 1 };
        string encoded = ForgeHash.HashPassword("x", strong);
        var desired = ForgeHashParameters.Development;
        Assert.False(ForgeHash.NeedsRehash(encoded, desired));
    }

    [Fact]
    public void Pepper_RoundTrips()
    {
        byte[] pepper = new byte[32];
        Random.Shared.NextBytes(pepper);
        byte[] password = "peppered"u8.ToArray();

        string encoded = ForgeHash.HashPassword(password, pepper, ForgeHashParameters.Development);
        Assert.True(ForgeHash.VerifyPassword(password, pepper, encoded));
        Assert.False(ForgeHash.VerifyPassword(password, encoded));
    }
}
