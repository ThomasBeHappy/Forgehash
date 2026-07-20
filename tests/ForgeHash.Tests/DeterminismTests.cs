using System.Text;

namespace ForgeHash.Tests;

public class DeterminismTests
{
    private static readonly ForgeHashParameters Dev = ForgeHashParameters.Development;

    [Fact]
    public void SameInputs_ProduceSameOutput()
    {
        byte[] password = Encoding.UTF8.GetBytes("password");
        byte[] salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

        byte[] a = ForgeHash.DeriveHash(password, salt, Dev);
        byte[] b = ForgeHash.DeriveHash(password, salt, Dev);

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSalts_ProduceDifferentOutputs()
    {
        byte[] password = Encoding.UTF8.GetBytes("password");
        byte[] salt1 = new byte[16];
        byte[] salt2 = new byte[16];
        salt2[0] = 1;

        byte[] a = ForgeHash.DeriveHash(password, salt1, Dev);
        byte[] b = ForgeHash.DeriveHash(password, salt2, Dev);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DifferentPasswords_ProduceDifferentOutputs()
    {
        byte[] salt = new byte[16];
        byte[] a = ForgeHash.DeriveHash("password"u8, salt, Dev);
        byte[] b = ForgeHash.DeriveHash("Password"u8, salt, Dev);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void EmptyPassword_IsSupported()
    {
        byte[] salt = new byte[16];
        byte[] hash = ForgeHash.DeriveHash(ReadOnlySpan<byte>.Empty, salt, Dev);
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void NullBytesInPassword_ArePreserved()
    {
        byte[] salt = new byte[16];
        byte[] withNull = [1, 0, 2, 0, 3];
        byte[] without = [1, 2, 3];

        byte[] a = ForgeHash.DeriveHash(withNull, salt, Dev);
        byte[] b = ForgeHash.DeriveHash(without, salt, Dev);

        Assert.NotEqual(a, b);
    }
}
