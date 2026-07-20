using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

namespace ForgeHash.X.Tests;

public sealed class EncodingTests
{
    [Fact]
    public void Encode_ProducesCanonicalFormat()
    {
        byte[] salt = new byte[16];
        byte[] hash = new byte[32];
        hash[0] = 0xAB;
        var parameters = new ForgeHashXParameters
        {
            MemoryKiB = 1024,
            Iterations = 1,
            Parallelism = 1,
            OutputLength = 32,
            SaltLength = 16,
        };

        string encoded = ForgeHashXEncoding.Encode(parameters, salt, hash);

        Assert.StartsWith("$forgehx$v=0$m=1024,t=1,p=1$", encoded, StringComparison.Ordinal);
        Assert.DoesNotContain("==", encoded, StringComparison.Ordinal);
        Assert.Equal(encoded, ForgeHashXEncoding.Parse(encoded).Encoded);
    }

    [Fact]
    public void HashPassword_RoundTripsThroughVerify()
    {
        string encoded = ForgeHashXApi.HashPassword("secret", ForgeHashXParameters.Toy);
        Assert.True(ForgeHashXApi.VerifyPassword("secret", encoded));
        Assert.False(ForgeHashXApi.VerifyPassword("Secret", encoded));
    }
}
