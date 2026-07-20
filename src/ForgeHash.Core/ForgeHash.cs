using System.Security.Cryptography;
using System.Text;

namespace ForgeHash;

/// <summary>
/// Public ForgeHash-B3 password hashing API.
/// </summary>
/// <remarks>
/// <para>
/// ForgeHash is experimental cryptographic software. It has not received sufficient
/// independent review and must not be used to protect production credentials.
/// </para>
/// <para>
/// String overloads encode passwords as UTF-8 without Unicode normalization.
/// Immutable caller-supplied strings cannot be cleared from memory by this library.
/// </para>
/// </remarks>
public static class ForgeHash
{
    /// <summary>
    /// Hashes a password with a freshly generated random salt and returns the canonical encoded string.
    /// </summary>
    public static string HashPassword(ReadOnlySpan<byte> password, ForgeHashParameters? parameters = null)
    {
        parameters ??= ForgeHashParameters.Interactive;
        ParameterValidator.ValidateForHashing(parameters);

        byte[] salt = new byte[parameters.SaltLength];
        RandomNumberGenerator.Fill(salt);

        try
        {
            byte[] hash = ForgeHashEngine.DeriveHash(password, salt, parameters);
            try
            {
                return ForgeHashEncoding.Encode(
                    ForgeHashEngine.AlgorithmVersion,
                    parameters.MemoryKiB,
                    parameters.Iterations,
                    parameters.Parallelism,
                    salt,
                    hash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    /// <summary>
    /// Hashes a UTF-8 password string. The password is not Unicode-normalized.
    /// </summary>
    public static string HashPassword(string password, ForgeHashParameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(password);
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return HashPassword(bytes, parameters);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>
    /// Hashes a password after applying the recommended pepper construction.
    /// </summary>
    public static string HashPassword(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> pepper,
        ForgeHashParameters? parameters = null)
    {
        byte[] effective = Blake3Adapter.ApplyPepper(password, pepper);
        try
        {
            return HashPassword(effective, parameters);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(effective);
        }
    }

    /// <summary>
    /// Verifies a password against an encoded ForgeHash string using fixed-time comparison.
    /// Returns <c>false</c> for malformed hashes rather than throwing.
    /// </summary>
    public static bool VerifyPassword(ReadOnlySpan<byte> password, string encodedHash)
    {
        if (!ForgeHashParser.TryParse(encodedHash, out ParsedForgeHash? parsed) || parsed is null)
        {
            return false;
        }

        return VerifyParsed(password, parsed);
    }

    /// <summary>
    /// Verifies a UTF-8 password string against an encoded hash.
    /// </summary>
    public static bool VerifyPassword(string password, string encodedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return VerifyPassword(bytes, encodedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>
    /// Verifies a password that was hashed with the recommended pepper construction.
    /// </summary>
    public static bool VerifyPassword(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> pepper,
        string encodedHash)
    {
        byte[] effective = Blake3Adapter.ApplyPepper(password, pepper);
        try
        {
            return VerifyPassword(effective, encodedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(effective);
        }
    }

    /// <summary>
    /// Returns whether an encoded hash should be rehashed under the desired policy.
    /// Stronger-than-desired costs alone do not require a rehash.
    /// </summary>
    public static bool NeedsRehash(string encodedHash, ForgeHashParameters desiredParameters)
    {
        ArgumentNullException.ThrowIfNull(desiredParameters);

        if (!ForgeHashParser.TryParse(encodedHash, out ParsedForgeHash? parsed) || parsed is null)
        {
            return true;
        }

        if (!string.Equals(parsed.AlgorithmId, ForgeHashEncoding.AlgorithmId, StringComparison.Ordinal))
        {
            return true;
        }

        if (parsed.Version < ForgeHashEngine.AlgorithmVersion)
        {
            return true;
        }

        if (parsed.MemoryKiB < desiredParameters.MemoryKiB)
        {
            return true;
        }

        if (parsed.Iterations < desiredParameters.Iterations)
        {
            return true;
        }

        if (parsed.Parallelism != desiredParameters.Parallelism)
        {
            return true;
        }

        if (parsed.Hash.Length != desiredParameters.OutputLength)
        {
            return true;
        }

        if (parsed.Salt.Length < desiredParameters.SaltLength)
        {
            return true;
        }

        if (!parsed.IsCanonical)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Low-level research API. Does not generate a salt automatically.
    /// </summary>
    public static byte[] DeriveHash(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashParameters parameters)
        => ForgeHashEngine.DeriveHash(password, salt, parameters);

    /// <summary>
    /// Low-level research API using the optional parallel lane implementation.
    /// Output must match the sequential reference for the same inputs.
    /// </summary>
    public static byte[] DeriveHashParallel(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashParameters parameters)
        => ForgeHashEngine.DeriveHash(password, salt, parameters, useParallelLanes: true);

    /// <summary>
    /// Computes the initial 32-byte seed for diagnostics and test-vector generation.
    /// </summary>
    public static byte[] ComputeSeed(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashParameters parameters)
        => ForgeHashEngine.ComputeSeed(password, salt, parameters);

    /// <summary>
    /// Derives a hash while recording block references for research tooling.
    /// </summary>
    public static byte[] DeriveHashWithTrace(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashParameters parameters,
        BlockReferenceObserver observer)
        => ForgeHashEngine.DeriveHash(password, salt, parameters, useParallelLanes: false, observer);

    private static bool VerifyParsed(ReadOnlySpan<byte> password, ParsedForgeHash parsed)
    {
        ForgeHashParameters parameters = parsed.ToParameters();
        byte[] actual;
        try
        {
            actual = ForgeHashEngine.DeriveHash(password, parsed.Salt, parameters);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (CryptographicException)
        {
            return false;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(actual, parsed.Hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
        }
    }
}
