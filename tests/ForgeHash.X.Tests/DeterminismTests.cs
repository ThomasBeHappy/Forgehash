using System.Text;
using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

namespace ForgeHash.X.Tests;

public sealed class DeterminismTests
{
    private static readonly ForgeHashXParameters Toy = ForgeHashXParameters.Toy;

    [Fact]
    public void SameInputs_ProduceSameOutput()
    {
        byte[] password = Encoding.UTF8.GetBytes("password");
        byte[] salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

        byte[] a = ForgeHashXApi.DeriveHash(password, salt, Toy);
        byte[] b = ForgeHashXApi.DeriveHash(password, salt, Toy);

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSalts_ProduceDifferentOutputs()
    {
        byte[] password = Encoding.UTF8.GetBytes("password");
        byte[] salt1 = new byte[16];
        byte[] salt2 = new byte[16];
        salt2[0] = 1;

        Assert.NotEqual(
            ForgeHashXApi.DeriveHash(password, salt1, Toy),
            ForgeHashXApi.DeriveHash(password, salt2, Toy));
    }

    [Fact]
    public void DifferentPasswords_ProduceDifferentOutputs()
    {
        byte[] salt = new byte[16];
        Assert.NotEqual(
            ForgeHashXApi.DeriveHash("password"u8, salt, Toy),
            ForgeHashXApi.DeriveHash("Password"u8, salt, Toy));
    }

    [Fact]
    public void EmptyPassword_IsSupported()
    {
        byte[] hash = ForgeHashXApi.DeriveHash(ReadOnlySpan<byte>.Empty, new byte[16], Toy);
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void NullBytesInPassword_ArePreserved()
    {
        byte[] salt = new byte[16];
        byte[] withNull = [1, 0, 2, 0, 3];
        byte[] without = [1, 2, 3];

        Assert.NotEqual(
            ForgeHashXApi.DeriveHash(withNull, salt, Toy),
            ForgeHashXApi.DeriveHash(without, salt, Toy));
    }
}
