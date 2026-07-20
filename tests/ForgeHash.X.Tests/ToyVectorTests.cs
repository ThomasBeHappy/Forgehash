using System.Text.Json;
using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;
using Xunit;

namespace ForgeHash.X.Tests;

/// <summary>
/// Pins ForgeHash-X v0 toy vectors. Experimental — not for production.
/// </summary>
public sealed class ToyVectorTests
{
    public static IEnumerable<object[]> VectorIds()
    {
        foreach (string id in new[]
                 {
                     "vector1_empty_password_zero_salt",
                     "vector2_short_password_incrementing_salt",
                     "vector3_two_lanes_toy",
                 })
        {
            yield return [id];
        }
    }

    [Theory]
    [MemberData(nameof(VectorIds))]
    public void MatchesFrozenVector(string id)
    {
        VectorCase vector = Load(id);
        var parameters = new ForgeHashXParameters
        {
            MemoryKiB = vector.MemoryKiB,
            Iterations = vector.Iterations,
            Parallelism = vector.Parallelism,
            OutputLength = vector.OutputLength,
            SaltLength = vector.Salt.Length,
        };

        byte[] seed = ForgeHashXApi.ComputeSeed(vector.Password, vector.Salt, parameters);
        byte[] hash = ForgeHashXApi.DeriveHash(vector.Password, vector.Salt, parameters);
        string encoded = ForgeHashXEncoding.Encode(parameters, vector.Salt, hash);

        Assert.Equal(vector.SeedHex, Convert.ToHexString(seed).ToLowerInvariant());
        Assert.Equal(vector.HashHex, Convert.ToHexString(hash).ToLowerInvariant());
        Assert.Equal(vector.Encoded, encoded);
        Assert.True(ForgeHashXApi.VerifyPassword(vector.Password, vector.Encoded));
    }

    [Fact]
    public void ParserRejectsB3Encoding()
    {
        Assert.Throws<FormatException>(() =>
            ForgeHashXEncoding.Parse("$forgeh$v=1$m=1024,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"));
    }

    [Fact]
    public void ParserRejectsWhitespace()
    {
        Assert.Throws<FormatException>(() =>
            ForgeHashXEncoding.Parse("$forgehx$v=0$m=1024,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA "));
    }

    [Fact]
    public void ParserRejectsLeadingZeroInCost()
    {
        Assert.Throws<FormatException>(() =>
            ForgeHashXEncoding.Parse("$forgehx$v=0$m=01024,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"));
    }

    private static VectorCase Load(string id)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "x0", "vectors", id + ".json");
        using FileStream stream = File.OpenRead(path);
        using JsonDocument doc = JsonDocument.Parse(stream);
        JsonElement root = doc.RootElement;
        return new VectorCase(
            root.GetProperty("passwordHex").GetString()!,
            root.GetProperty("saltHex").GetString()!,
            root.GetProperty("memoryKiB").GetInt32(),
            root.GetProperty("iterations").GetInt32(),
            root.GetProperty("parallelism").GetInt32(),
            root.GetProperty("outputLength").GetInt32(),
            root.GetProperty("seedHex").GetString()!,
            root.GetProperty("hashHex").GetString()!,
            root.GetProperty("encoded").GetString()!);
    }

    private sealed record VectorCase(
        string PasswordHex,
        string SaltHex,
        int MemoryKiB,
        int Iterations,
        int Parallelism,
        int OutputLength,
        string SeedHex,
        string HashHex,
        string Encoded)
    {
        public byte[] Password => Convert.FromHexString(PasswordHex);
        public byte[] Salt => Convert.FromHexString(SaltHex);
    }
}
