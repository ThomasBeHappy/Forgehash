using System.Text.RegularExpressions;

namespace ForgeHashX;

/// <summary>Canonical <c>$forgehx$v=0$…</c> encoding / parsing.</summary>
public static class ForgeHashXEncoding
{
    public const string AlgorithmId = "forgehx";
    public const int Version = 0;

    public static string Encode(ForgeHashXParameters parameters, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> hash)
    {
        parameters.Validate();
        return $"${AlgorithmId}$v={Version}$m={parameters.MemoryKiB},t={parameters.Iterations},p={parameters.Parallelism}${B64(salt)}${B64(hash)}";
    }

    public static ParsedForgeHashX Parse(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        if (encoded.Contains('\0') || Regex.IsMatch(encoded, @"\s"))
        {
            throw new FormatException("Whitespace or null in encoded hash.");
        }

        string[] parts = encoded.Split('$');
        if (parts.Length != 6 || parts[0] != "")
        {
            throw new FormatException("Malformed encoded hash.");
        }

        if (parts[1] != AlgorithmId)
        {
            throw new FormatException("Unsupported algorithm id.");
        }

        if (!parts[2].StartsWith("v=", StringComparison.Ordinal))
        {
            throw new FormatException("Malformed version.");
        }

        int version = ParseStrictNonNegativeInt(parts[2].AsSpan(2));
        if (version != Version)
        {
            throw new FormatException("Unsupported version.");
        }

        string[] costs = parts[3].Split(',');
        if (costs.Length != 3)
        {
            throw new FormatException("Malformed cost parameters.");
        }

        int memory = ParseCost(costs[0], "m");
        int iterations = ParseCost(costs[1], "t");
        int parallelism = ParseCost(costs[2], "p");
        byte[] salt = B64Decode(parts[4]);
        byte[] hash = B64Decode(parts[5]);
        if (hash.Length < ForgeHashXParameters.MinimumOutputLength ||
            hash.Length > ForgeHashXParameters.MaximumOutputLength)
        {
            throw new FormatException("Invalid hash length.");
        }

        var parameters = new ForgeHashXParameters
        {
            MemoryKiB = memory,
            Iterations = iterations,
            Parallelism = parallelism,
            OutputLength = hash.Length,
            SaltLength = salt.Length,
        };
        parameters.Validate();

        string canonical = Encode(parameters, salt, hash);
        if (canonical != encoded)
        {
            throw new FormatException("Encoded hash is not canonical.");
        }

        return new ParsedForgeHashX(version, parameters, salt, hash, canonical);
    }

    private static int ParseCost(string segment, string name)
    {
        int eq = segment.IndexOf('=');
        if (eq < 0 || segment[..eq] != name)
        {
            throw new FormatException($"Expected field '{name}'.");
        }

        return ParseStrictPositiveInt(segment.AsSpan(eq + 1));
    }

    private static int ParseStrictPositiveInt(ReadOnlySpan<char> text)
    {
        int value = ParseStrictNonNegativeInt(text);
        if (value == 0)
        {
            throw new FormatException("Integer out of range.");
        }

        return value;
    }

    private static int ParseStrictNonNegativeInt(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text[0] is '+' or '-' || !IsAllDigits(text))
        {
            throw new FormatException("Invalid integer field.");
        }

        // Allow a single "0"; reject leading zeros on multi-digit values.
        if (text.Length > 1 && text[0] == '0')
        {
            throw new FormatException("Leading zero not allowed.");
        }

        if (!int.TryParse(text, out int value) || value < 0)
        {
            throw new FormatException("Integer out of range.");
        }

        return value;
    }

    private static bool IsAllDigits(ReadOnlySpan<char> text)
    {
        foreach (char c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static string B64(ReadOnlySpan<byte> data)
        => Convert.ToBase64String(data).TrimEnd('=');

    private static byte[] B64Decode(string text)
    {
        if (text.Length == 0 || !Regex.IsMatch(text, @"^[A-Za-z0-9+/]+$"))
        {
            throw new FormatException("Malformed base64.");
        }

        int rem = text.Length % 4;
        if (rem == 1)
        {
            throw new FormatException("Malformed base64 length.");
        }

        string padded = text + rem switch
        {
            2 => "==",
            3 => "=",
            _ => "",
        };

        byte[] decoded = Convert.FromBase64String(padded);
        if (B64(decoded) != text)
        {
            throw new FormatException("Non-canonical base64.");
        }

        return decoded;
    }
}

/// <summary>Parsed <c>$forgehx$</c> string.</summary>
public sealed record ParsedForgeHashX(
    int Version,
    ForgeHashXParameters Parameters,
    byte[] Salt,
    byte[] Hash,
    string Encoded);
