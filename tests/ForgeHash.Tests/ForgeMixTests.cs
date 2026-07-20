using System.Buffers.Binary;

namespace ForgeHash.Tests;

public class ForgeMixTests
{
    [Fact]
    public void QuarterRound_IsDeterministic()
    {
        ulong a = 1, b = 2, c = 3, d = 4;
        ulong a2 = 1, b2 = 2, c2 = 3, d2 = 4;

        ForgeMix.QuarterRound(ref a, ref b, ref c, ref d);
        ForgeMix.QuarterRound(ref a2, ref b2, ref c2, ref d2);

        Assert.Equal(a, a2);
        Assert.Equal(b, b2);
        Assert.Equal(c, c2);
        Assert.Equal(d, d2);
    }

    [Fact]
    public void Mix_IsDeterministic_AndDependsOnPosition()
    {
        ulong[] input = new ulong[128];
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = (ulong)i * 0x9E3779B97F4A7C15UL;
        }

        ulong[] out1 = new ulong[128];
        ulong[] out2 = new ulong[128];
        ulong[] outDifferentPass = new ulong[128];

        ForgeMix.Mix(input, 0, 0, 2, out1);
        ForgeMix.Mix(input, 0, 0, 2, out2);
        ForgeMix.Mix(input, 1, 0, 2, outDifferentPass);

        Assert.Equal(out1, out2);
        Assert.NotEqual(out1, outDifferentPass);
    }

    [Fact]
    public void Mix_FeedForward_ChangesWhenInputChanges()
    {
        byte[] input = new byte[1024];
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = (byte)i;
        }

        byte[] a = new byte[1024];
        byte[] b = new byte[1024];
        ForgeMix.Mix(input, 0, 0, 0, a);

        input[0] ^= 0x01;
        ForgeMix.Mix(input, 0, 0, 0, b);

        Assert.False(a.AsSpan().SequenceEqual(b));
    }

    [Fact]
    public void Permutation_IsCompleteBijection()
    {
        // destinationIndex = (sourceIndex * 73 + 19) mod 128
        HashSet<int> seen = [];
        for (int source = 0; source < 128; source++)
        {
            int dest = ((source * 73) + 19) & 127;
            Assert.True(seen.Add(dest));
        }

        Assert.Equal(128, seen.Count);
    }

    [Fact]
    public void ByteAndWordApis_Agree()
    {
        byte[] inputBytes = new byte[1024];
        Random.Shared.NextBytes(inputBytes);

        ulong[] inputWords = new ulong[128];
        for (int i = 0; i < 128; i++)
        {
            inputWords[i] = BinaryPrimitives.ReadUInt64LittleEndian(inputBytes.AsSpan(i * 8, 8));
        }

        ulong[] outWords = new ulong[128];
        byte[] outBytes = new byte[1024];
        byte[] roundTrip = new byte[1024];

        ForgeMix.Mix(inputWords, 3, 1, 42, outWords);
        ForgeMix.Mix(inputBytes, 3, 1, 42, outBytes);

        for (int i = 0; i < 128; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(roundTrip.AsSpan(i * 8, 8), outWords[i]);
        }

        Assert.Equal(outBytes, roundTrip);
    }
}
