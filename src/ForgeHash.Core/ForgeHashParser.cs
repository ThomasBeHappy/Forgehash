using System.Globalization;

namespace ForgeHash;

/// <summary>
/// Defensive parser for attacker-controlled ForgeHash encoded strings.
/// Validates all fields before any memory allocation or expensive computation.
/// </summary>
public static class ForgeHashParser
{
    /// <summary>
    /// Attempts to parse an encoded hash without throwing.
    /// </summary>
    public static bool TryParse(string? encodedHash, out ParsedForgeHash? parsedHash)
        => TryParse(encodedHash, ParameterValidator.Limits.Default, out parsedHash);

    /// <summary>
    /// Attempts to parse an encoded hash using the supplied application limits.
    /// </summary>
    public static bool TryParse(
        string? encodedHash,
        ParameterValidator.Limits limits,
        out ParsedForgeHash? parsedHash)
    {
        parsedHash = null;
        if (encodedHash is null)
        {
            return false;
        }

        try
        {
            parsedHash = Parse(encodedHash, limits);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parses an encoded hash or throws <see cref="FormatException"/>.
    /// </summary>
    public static ParsedForgeHash Parse(string encodedHash, ParameterValidator.Limits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(encodedHash);
        limits ??= ParameterValidator.Limits.Default;

        // Reject embedded nulls and whitespace early — encoded hashes are attacker-controlled.
        if (encodedHash.Contains('\0', StringComparison.Ordinal) ||
            encodedHash.AsSpan().ContainsAny(" \t\r\n"))
        {
            throw new FormatException("Encoded hash must not contain whitespace or null characters.");
        }

        string[] parts = encodedHash.Split('$', StringSplitOptions.None);
        // "", "forgeh", "v=1", "m=...,t=...,p=...", salt, hash
        if (parts.Length != 6 || parts[0].Length != 0)
        {
            throw new FormatException("Encoded hash has an invalid number of fields.");
        }

        if (!string.Equals(parts[1], ForgeHashEncoding.AlgorithmId, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unknown algorithm identifier '{parts[1]}'.");
        }

        int version = ParseVersion(parts[2]);
        if (version != 1)
        {
            throw new NotSupportedException($"Unsupported ForgeHash version: {version}.");
        }

        ParseCostParameters(parts[3], out int memoryKiB, out int iterations, out int parallelism);

        byte[] salt = ForgeHashEncoding.DecodeBase64(parts[4]);
        byte[] hash = ForgeHashEncoding.DecodeBase64(parts[5]);

        if (hash.Length != ForgeHashParameters.DefaultOutputLength)
        {
            throw new FormatException("Version 1 hash digests must be exactly 32 bytes.");
        }

        // Validate before any caller can allocate based on these values.
        ParameterValidator.ValidateForVerification(
            memoryKiB,
            iterations,
            parallelism,
            hash.Length,
            salt.Length,
            limits);

        string canonical = ForgeHashEncoding.Encode(version, memoryKiB, iterations, parallelism, salt, hash);
        bool isCanonical = string.Equals(encodedHash, canonical, StringComparison.Ordinal);

        // Non-canonical forms of otherwise valid numbers (leading zeroes, reordering)
        // are already rejected by ParseCostParameters / ParseVersion. Any remaining
        // mismatch indicates alternate Base64 or unexpected formatting.
        if (!isCanonical)
        {
            throw new FormatException("Encoded hash is not in canonical form.");
        }

        return new ParsedForgeHash(
            ForgeHashEncoding.AlgorithmId,
            version,
            memoryKiB,
            iterations,
            parallelism,
            salt,
            hash,
            canonical,
            isCanonical: true);
    }

    private static int ParseVersion(string field)
    {
        if (!field.StartsWith("v=", StringComparison.Ordinal))
        {
            throw new FormatException("Missing version field.");
        }

        ReadOnlySpan<char> value = field.AsSpan(2);
        return ParseStrictPositiveInt(value, "version");
    }

    private static void ParseCostParameters(
        string field,
        out int memoryKiB,
        out int iterations,
        out int parallelism)
    {
        // Strict order: m=<>,t=<>,p=<>
        string[] segments = field.Split(',', StringSplitOptions.None);
        if (segments.Length != 3)
        {
            throw new FormatException("Cost parameter field is malformed.");
        }

        memoryKiB = ParsePrefixedParameter(segments[0], "m");
        iterations = ParsePrefixedParameter(segments[1], "t");
        parallelism = ParsePrefixedParameter(segments[2], "p");
    }

    private static int ParsePrefixedParameter(string segment, string expectedName)
    {
        int eq = segment.IndexOf('=');
        if (eq <= 0 || eq == segment.Length - 1)
        {
            throw new FormatException("Cost parameter field is malformed.");
        }

        string name = segment[..eq];
        if (!string.Equals(name, expectedName, StringComparison.Ordinal))
        {
            throw new FormatException($"Expected cost parameter '{expectedName}' in canonical order.");
        }

        return ParseStrictPositiveInt(segment.AsSpan(eq + 1), expectedName);
    }

    private static int ParseStrictPositiveInt(ReadOnlySpan<char> text, string fieldName)
    {
        if (text.IsEmpty)
        {
            throw new FormatException($"Missing {fieldName} value.");
        }

        if (text[0] == '+' || text[0] == '-')
        {
            throw new FormatException($"Invalid {fieldName} value.");
        }

        // Reject leading zeroes except for the value zero itself (which is also invalid).
        if (text.Length > 1 && text[0] == '0')
        {
            throw new FormatException($"Invalid {fieldName} value: leading zeroes are not allowed.");
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            throw new FormatException($"Invalid {fieldName} value.");
        }

        if (value <= 0)
        {
            throw new FormatException($"{fieldName} must be a positive integer.");
        }

        return value;
    }
}
