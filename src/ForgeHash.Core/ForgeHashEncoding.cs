using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace ForgeHash;

/// <summary>
/// Canonical ForgeHash encoded-string formatting.
/// </summary>
public static class ForgeHashEncoding
{
    /// <summary>Compact algorithm identifier.</summary>
    public const string AlgorithmId = "forgeh";

    /// <summary>Encodes a salt and digest into the canonical <c>$forgeh$...</c> string.</summary>
    public static string Encode(
        int version,
        int memoryKiB,
        int iterations,
        int parallelism,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> hash)
    {
        if (version != 1)
        {
            throw new NotSupportedException($"Unsupported ForgeHash version: {version}.");
        }

        if (hash.Length != ForgeHashParameters.DefaultOutputLength)
        {
            throw new ArgumentOutOfRangeException(nameof(hash), "Version 1 encoded hashes require a 32-byte digest.");
        }

        string saltB64 = EncodeBase64(salt);
        string hashB64 = EncodeBase64(hash);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"${AlgorithmId}$v={version}$m={memoryKiB},t={iterations},p={parallelism}${saltB64}${hashB64}");
    }

    /// <summary>RFC 4648 Base64 without padding.</summary>
    public static string EncodeBase64(ReadOnlySpan<byte> data)
    {
        int maxLength = Base64.GetMaxEncodedToUtf8Length(data.Length);
        Span<byte> utf8 = maxLength <= 512 ? stackalloc byte[maxLength] : new byte[maxLength];
        OperationStatus status = Base64.EncodeToUtf8(data, utf8, out _, out int bytesWritten, isFinalBlock: true);
        if (status != OperationStatus.Done)
        {
            throw new InvalidOperationException("Base64 encoding failed.");
        }

        // Strip padding if the encoder produced it.
        while (bytesWritten > 0 && utf8[bytesWritten - 1] == (byte)'=')
        {
            bytesWritten--;
        }

        return Encoding.ASCII.GetString(utf8[..bytesWritten]);
    }

    /// <summary>Decodes unpadded RFC 4648 Base64.</summary>
    public static byte[] DecodeBase64(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            throw new FormatException("Base64 value must not be empty.");
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '=' || char.IsWhiteSpace(c) || c == '\0')
            {
                throw new FormatException("Malformed Base64 value.");
            }
        }

        // Restore canonical padding length for the framework decoder.
        int pad = (4 - (text.Length % 4)) % 4;
        if (pad == 3)
        {
            // Length mod 4 == 1 is never valid Base64.
            throw new FormatException("Malformed Base64 value.");
        }

        char[] padded = new char[text.Length + pad];
        text.CopyTo(padded);
        for (int i = 0; i < pad; i++)
        {
            padded[text.Length + i] = '=';
        }

        try
        {
            return Convert.FromBase64CharArray(padded, 0, padded.Length);
        }
        catch (FormatException ex)
        {
            throw new FormatException("Malformed Base64 value.", ex);
        }
    }
}
