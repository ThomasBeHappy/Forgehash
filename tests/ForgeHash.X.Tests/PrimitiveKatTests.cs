using System.Text.Json;
using ForgeHashX;

namespace ForgeHash.X.Tests;

/// <summary>
/// Known-answer tests for ForgePerm + ForgeX sponge (implementers/x0/kats).
/// </summary>
public class PrimitiveKatTests
{
    private static readonly string KatPath = Path.Combine(
        AppContext.BaseDirectory, "x0", "kats", "forgex_primitive.json");

    [Fact]
    public void RoundConstants_MatchFrozenKat()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(KatPath));
        var rc = doc.RootElement.GetProperty("roundConstants");
        Assert.Equal(rc.GetProperty("r0_i0").GetString(), ForgePerm.RoundConstant(0, 0).ToString("x16"));
        Assert.Equal(rc.GetProperty("r7_i15").GetString(), ForgePerm.RoundConstant(7, 15).ToString("x16"));
    }

    [Fact]
    public void ZeroStatePermute_MatchesFrozenKat()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(KatPath));
        var expected = doc.RootElement.GetProperty("zeroStatePermute")
            .EnumerateArray()
            .Select(e => Convert.ToUInt64(e.GetString(), 16))
            .ToArray();

        Span<ulong> state = stackalloc ulong[16];
        ForgePerm.Permute(state);

        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(expected[i], state[i]);
        }
    }

    [Fact]
    public void ForgeX_HashAndXof_MatchFrozenKats()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(KatPath));

        foreach (var item in doc.RootElement.GetProperty("hash").EnumerateArray())
        {
            string tag = item.GetProperty("domainTag").GetString()!;
            byte[] data = Convert.FromHexString(item.GetProperty("dataHex").GetString()!);
            string expected = item.GetProperty("outHex").GetString()!;
            byte[] actual = ForgeX.Hash(tag, data);
            Assert.Equal(expected, Convert.ToHexString(actual).ToLowerInvariant());
        }

        foreach (var item in doc.RootElement.GetProperty("xof").EnumerateArray())
        {
            string tag = item.GetProperty("domainTag").GetString()!;
            byte[] data = Convert.FromHexString(item.GetProperty("dataHex").GetString()!);
            int length = item.GetProperty("length").GetInt32();
            string expected = item.GetProperty("outHex").GetString()!;
            byte[] actual = ForgeX.Xof(tag, data, length);
            Assert.Equal(expected, Convert.ToHexString(actual).ToLowerInvariant());
        }
    }
}
