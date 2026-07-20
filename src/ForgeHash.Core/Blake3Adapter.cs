using System.Security.Cryptography;
using Blake3;
using ForgeHash.Internal;

namespace ForgeHash;

/// <summary>
/// Thin adapter around the official BLAKE3 derive-key, hash, keyed-hash, and XOF operations.
/// </summary>
internal static class Blake3Adapter
{
    internal const string SeedContext = "ForgeHash/v1/seed";
    internal const string ExpandPrefix = "ForgeHash/v1/expand";
    internal const string GroupPrefix = "ForgeHash/v1/group";
    internal const string GroupRootPrefix = "ForgeHash/v1/group-root";
    internal const string FinalPrefix = "ForgeHash/v1/final";
    internal const string OutputPrefix = "ForgeHash/v1/output";
    internal const string PepperPrefix = "ForgeHash/v1/pepper";

    /// <summary>
    /// BLAKE3 derive-key with context <c>ForgeHash/v1/seed</c>.
    /// </summary>
    internal static byte[] DeriveSeed(ReadOnlySpan<byte> material)
    {
        using Hasher hasher = Hasher.NewDeriveKey(SeedContext);
        hasher.Update(material);
        Hash hash = hasher.Finalize();
        return hash.AsSpan().ToArray();
    }

    /// <summary>
    /// Domain-separated BLAKE3 XOF used by Expand.
    /// </summary>
    internal static void Expand(ReadOnlySpan<byte> input, Span<byte> output)
    {
        using Hasher hasher = Hasher.New();
        hasher.Update(BinaryEncoding.Utf8(ExpandPrefix));
        hasher.Update(input);
        hasher.Finalize(output);
    }

    internal static byte[] Expand(ReadOnlySpan<byte> input, int outputLength)
    {
        byte[] output = new byte[outputLength];
        Expand(input, output);
        return output;
    }

    internal static byte[] Hash(ReadOnlySpan<byte> input)
    {
        Hash hash = Hasher.Hash(input);
        return hash.AsSpan().ToArray();
    }

    internal static void HashInto(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (output.Length != 32)
        {
            throw new ArgumentOutOfRangeException(nameof(output), "BLAKE3 digests are 32 bytes.");
        }

        using Hasher hasher = Hasher.New();
        hasher.Update(input);
        hasher.Finalize(output);
    }

    internal static byte[] KeyedHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
    {
        if (key.Length != 32)
        {
            throw new ArgumentOutOfRangeException(nameof(key), "BLAKE3 keyed hash requires a 32-byte key.");
        }

        using Hasher hasher = Hasher.NewKeyed(key);
        hasher.Update(input);
        Hash hash = hasher.Finalize();
        return hash.AsSpan().ToArray();
    }

    internal static byte[] Xof(ReadOnlySpan<byte> input, int outputLength)
    {
        byte[] output = new byte[outputLength];
        using Hasher hasher = Hasher.New();
        hasher.Update(input);
        hasher.Finalize(output);
        return output;
    }

    /// <summary>
    /// Applies the recommended pepper construction and returns a 32-byte effective password.
    /// </summary>
    internal static byte[] ApplyPepper(ReadOnlySpan<byte> password, ReadOnlySpan<byte> pepper)
    {
        if (pepper.Length != 32)
        {
            throw new ArgumentOutOfRangeException(nameof(pepper), "Pepper must be exactly 32 bytes.");
        }

        checked
        {
            int length = BinaryEncoding.Utf8(PepperPrefix).Length + 8 + password.Length;
            byte[] buffer = new byte[length];
            byte[] prefix = BinaryEncoding.Utf8(PepperPrefix);
            prefix.AsSpan().CopyTo(buffer);
            BinaryEncoding.WriteInt64(buffer, prefix.Length, password.Length);
            password.CopyTo(buffer.AsSpan(prefix.Length + 8));

            try
            {
                return KeyedHash(pepper, buffer);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(buffer);
                CryptographicOperations.ZeroMemory(prefix);
            }
        }
    }
}
