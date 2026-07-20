namespace ForgeHash.Tests;

public class EncodingTests
{
    [Fact]
    public void Encode_ProducesCanonicalFormat()
    {
        byte[] salt = new byte[16];
        byte[] hash = new byte[32];
        hash[0] = 0xAB;

        string encoded = ForgeHashEncoding.Encode(1, 65536, 3, 1, salt, hash);

        Assert.StartsWith("$forgeh$v=1$m=65536,t=3,p=1$", encoded, StringComparison.Ordinal);
        Assert.DoesNotContain("==", encoded, StringComparison.Ordinal);
    }

    [Fact]
    public void Base64_RoundTrips()
    {
        byte[] data = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        string encoded = ForgeHashEncoding.EncodeBase64(data);
        Assert.DoesNotContain('=', encoded);
        Assert.Equal(data, ForgeHashEncoding.DecodeBase64(encoded));
    }

    [Fact]
    public void HashPassword_RoundTripsThroughVerify()
    {
        string encoded = ForgeHash.HashPassword("secret", ForgeHashParameters.Development);
        Assert.True(ForgeHash.VerifyPassword("secret", encoded));
        Assert.False(ForgeHash.VerifyPassword("Secret", encoded));
    }
}
