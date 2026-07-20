namespace ForgeHash.Tests;

public class ParserAttackTests
{
    [Fact]
    public void ExcessiveMemory_IsRejectedWithoutAllocatingMatrix()
    {
        byte[] salt = new byte[16];
        byte[] hash = new byte[32];
        string encoded =
            $"$forgeh$v=1$m=2147483647,t=1,p=1${ForgeHashEncoding.EncodeBase64(salt)}${ForgeHashEncoding.EncodeBase64(hash)}";

        long before = GC.GetTotalMemory(forceFullCollection: true);
        Assert.False(ForgeHashParser.TryParse(encoded, out _));
        Assert.False(ForgeHash.VerifyPassword("x", encoded));
        long after = GC.GetTotalMemory(forceFullCollection: false);

        // Parsing/verify must fail cheaply; allow some GC noise but not multi-GiB growth.
        Assert.True(after - before < 32 * 1024 * 1024, $"Unexpected allocation growth: {after - before}");
    }

    [Theory]
    [InlineData("$forgeh$v=1$m=8192,t=1,p=1$AAAA$AAAA$EXTRA")]
    [InlineData("$forgeh$v=1$m=8192,t=1,p=1,m=8192$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$forgeh$v=1$m=+8192,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$forgeh$v=1$m=8192,t=-1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$forgeh$v=1$m=8192,t=1,p=1$@@@$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$forgeh$v=1$m=8192,t=99,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("forgeh$v=1$m=8192,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void MaliciousEncodings_AreRejected(string encoded)
    {
        Assert.False(ForgeHashParser.TryParse(encoded, out _));
        Assert.False(ForgeHash.VerifyPassword("password", encoded));
    }

    [Fact]
    public void Parse_DoesNotThrow_OnHugeInputString()
    {
        string encoded = "$forgeh$v=1$m=8192,t=1,p=1$" + new string('A', 1_000_000);
        Assert.False(ForgeHashParser.TryParse(encoded, out _));
    }
}
