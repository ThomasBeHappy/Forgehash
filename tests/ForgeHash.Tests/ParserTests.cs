namespace ForgeHash.Tests;

public class ParserTests
{
    private static string ValidHash()
        => ForgeHash.HashPassword("parser-test", ForgeHashParameters.Development);

    [Fact]
    public void Parse_AcceptsCanonicalHash()
    {
        string encoded = ValidHash();
        ParsedForgeHash parsed = ForgeHashParser.Parse(encoded);

        Assert.Equal("forgeh", parsed.AlgorithmId);
        Assert.Equal(1, parsed.Version);
        Assert.Equal(8192, parsed.MemoryKiB);
        Assert.True(parsed.IsCanonical);
    }

    [Theory]
    [InlineData("$forgeh$v=1$t=3,m=65536,p=1$AAAA$AAAA")] // reordered
    [InlineData("$forgeh$v=01$m=65536,t=3,p=1$AAAA$AAAA")] // leading zero version
    [InlineData("$forgeh$v=1$m=065536,t=3,p=1$AAAA$AAAA")] // leading zero memory
    [InlineData("$forgeh$v=1$m=65536, t=3,p=1$AAAA$AAAA")] // whitespace
    [InlineData("$forgeh$v=1$m=65536,t=3,p=1$AAAA")] // missing hash
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$AAAA$AAAA")] // wrong algorithm
    [InlineData("$forgeh$v=2$m=8192,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // unsupported version (salt/hash lengths approx)
    public void Parse_RejectsMalformed(string encoded)
    {
        Assert.False(ForgeHashParser.TryParse(encoded, out _));
    }

    [Fact]
    public void Parse_RejectsExcessiveMemoryBeforeAllocation()
    {
        // 32-byte salt and hash of correct decoded length, but memory above policy.
        byte[] salt = new byte[16];
        byte[] hash = new byte[32];
        string saltB64 = ForgeHashEncoding.EncodeBase64(salt);
        string hashB64 = ForgeHashEncoding.EncodeBase64(hash);
        string encoded = $"$forgeh$v=1$m=2000000,t=1,p=1${saltB64}${hashB64}";

        Assert.False(ForgeHashParser.TryParse(encoded, out _));
    }

    [Fact]
    public void Parse_RejectsZeroIterations()
    {
        byte[] salt = new byte[16];
        byte[] hash = new byte[32];
        string encoded = $"$forgeh$v=1$m=8192,t=0,p=1${ForgeHashEncoding.EncodeBase64(salt)}${ForgeHashEncoding.EncodeBase64(hash)}";
        Assert.False(ForgeHashParser.TryParse(encoded, out _));
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForNull()
    {
        Assert.False(ForgeHashParser.TryParse(null, out _));
    }
}
