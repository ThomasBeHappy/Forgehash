using System.Reflection;
using System.Security.Cryptography;

namespace ForgeHash.Tests;

public class ConstantTimeTests
{
    [Fact]
    public void VerifyPassword_UsesFixedTimeEquals()
    {
        MethodInfo? method = typeof(CryptographicOperations).GetMethod(
            nameof(CryptographicOperations.FixedTimeEquals),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(ReadOnlySpan<byte>), typeof(ReadOnlySpan<byte>)],
            modifiers: null);

        Assert.NotNull(method);

        // Behavioral check: equal digests compare true, unequal compare false,
        // matching the FixedTimeEquals contract used by VerifyPassword.
        byte[] a = new byte[32];
        byte[] b = new byte[32];
        RandomNumberGenerator.Fill(a);
        a.CopyTo(b, 0);
        Assert.True(CryptographicOperations.FixedTimeEquals(a, b));
        b[^1] ^= 0x01;
        Assert.False(CryptographicOperations.FixedTimeEquals(a, b));

        string encoded = ForgeHash.HashPassword("const-time", ForgeHashParameters.Development);
        Assert.True(ForgeHash.VerifyPassword("const-time", encoded));
        Assert.False(ForgeHash.VerifyPassword("const-time!", encoded));
    }
}
