using System.Security.Cryptography;
using System.Text;

namespace ForgeHashX;

/// <summary>
/// Experimental ForgeHash-X v0 API. Custom ForgeX sponge — no BLAKE3.
/// Not for production password storage. Not compatible with ForgeHash-B3.
/// </summary>
public static class ForgeHashX
{
    public static byte[] ComputeSeed(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashXParameters parameters)
        => ForgeHashXEngine.ComputeSeed(password, salt, parameters);

    public static byte[] DeriveHash(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashXParameters parameters)
        => ForgeHashXEngine.DeriveHash(password, salt, parameters, useParallelLanes: false);

    /// <summary>
    /// Same as <see cref="DeriveHash"/> but fills lanes in parallel within each slice
    /// (barrier between slices). Must match sequential byte-for-byte.
    /// </summary>
    public static byte[] DeriveHashParallel(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashXParameters parameters)
        => ForgeHashXEngine.DeriveHash(password, salt, parameters, useParallelLanes: true);

    public static string HashPassword(ReadOnlySpan<byte> password, ForgeHashXParameters? parameters = null)
    {
        parameters ??= ForgeHashXParameters.Toy;
        parameters.Validate();
        byte[] salt = RandomNumberGenerator.GetBytes(parameters.SaltLength);
        byte[] hash = DeriveHash(password, salt, parameters);
        return ForgeHashXEncoding.Encode(parameters, salt, hash);
    }

    public static string HashPassword(string password, ForgeHashXParameters? parameters = null)
        => HashPassword(Encoding.UTF8.GetBytes(password), parameters);

    public static bool VerifyPassword(ReadOnlySpan<byte> password, string encoded)
    {
        ParsedForgeHashX parsed;
        try
        {
            parsed = ForgeHashXEncoding.Parse(encoded);
        }
        catch
        {
            return false;
        }

        byte[] actual;
        try
        {
            actual = DeriveHash(password, parsed.Salt, parsed.Parameters);
        }
        catch
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(actual, parsed.Hash);
    }

    public static bool VerifyPassword(string password, string encoded)
        => VerifyPassword(Encoding.UTF8.GetBytes(password), encoded);
}
