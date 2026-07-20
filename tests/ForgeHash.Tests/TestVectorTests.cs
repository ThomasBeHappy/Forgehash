namespace ForgeHash.Tests;

/// <summary>
/// Pins the official ForgeHash-B3 v1 test vectors. Any mismatch means the algorithm changed.
/// </summary>
public class TestVectorTests
{
    public static TheoryData<string> VectorIds()
        => ["V1", "V2", "V3", "V4"];

    [Theory]
    [MemberData(nameof(VectorIds))]
    public void OfficialVector_MatchesPinnedDigestAndEncoding(string id)
    {
        VectorCase vector = Load(id);
        TestVectorSnapshot actual = ForgeHashTestVectors.Generate(
            vector.Name,
            Convert.FromHexString(vector.PasswordHex),
            Convert.FromHexString(vector.SaltHex),
            vector.Parameters);

        Assert.Equal(vector.SeedHex, Hex(actual.Seed));
        Assert.Equal(vector.GroupRootHex, Hex(actual.GroupRoot));
        Assert.Equal(vector.HashHex, Hex(actual.Hash));
        Assert.Equal(vector.Encoded, actual.Encoded);

        Assert.Equal(vector.InitializedBlocks.Length, actual.InitializedBlocks.Count);
        for (int i = 0; i < vector.InitializedBlocks.Length; i++)
        {
            var expected = vector.InitializedBlocks[i];
            SampledBlock got = actual.InitializedBlocks[i];
            Assert.Equal(expected.Pass, got.Pass);
            Assert.Equal(expected.Lane, got.Lane);
            Assert.Equal(expected.BlockIndex, got.BlockIndex);
            Assert.Equal(expected.PrefixHex, Hex(got.Prefix));
        }

        Assert.Equal(vector.SampleReferences.Length, actual.SampleReferences.Count);
        for (int i = 0; i < vector.SampleReferences.Length; i++)
        {
            var expected = vector.SampleReferences[i];
            BlockReferenceTrace got = actual.SampleReferences[i];
            Assert.Equal(expected.Pass, got.Pass);
            Assert.Equal(expected.Lane, got.Lane);
            Assert.Equal(expected.BlockIndex, got.BlockIndex);
            Assert.Equal(expected.PreviousIndex, got.PreviousIndex);
            Assert.Equal(expected.ReferenceLane, got.ReferenceLane);
            Assert.Equal(expected.ReferenceIndex, got.ReferenceIndex);
            Assert.Equal(expected.CrossLane, got.CrossLane);
        }

        Assert.Equal(vector.ForgeMixSamples.Length, actual.ForgeMixSamples.Count);
        for (int i = 0; i < vector.ForgeMixSamples.Length; i++)
        {
            var expected = vector.ForgeMixSamples[i];
            SampledBlock got = actual.ForgeMixSamples[i];
            Assert.Equal(expected.Pass, got.Pass);
            Assert.Equal(expected.Lane, got.Lane);
            Assert.Equal(expected.BlockIndex, got.BlockIndex);
            Assert.Equal(expected.PrefixHex, Hex(got.Prefix));
        }
    }

    [Theory]
    [MemberData(nameof(VectorIds))]
    public void OfficialVector_VerifyPassword_Succeeds(string id)
    {
        VectorCase vector = Load(id);
        byte[] password = Convert.FromHexString(vector.PasswordHex);
        Assert.True(ForgeHash.VerifyPassword(password, vector.Encoded));
    }

    [Theory]
    [MemberData(nameof(VectorIds))]
    public void OfficialVector_ParallelMatchesSequential(string id)
    {
        VectorCase vector = Load(id);
        if (vector.Parameters.Parallelism == 1)
        {
            return;
        }

        byte[] password = Convert.FromHexString(vector.PasswordHex);
        byte[] salt = Convert.FromHexString(vector.SaltHex);
        byte[] sequential = ForgeHash.DeriveHash(password, salt, vector.Parameters);
        byte[] parallel = ForgeHash.DeriveHashParallel(password, salt, vector.Parameters);

        Assert.Equal(Hex(sequential), vector.HashHex);
        Assert.Equal(sequential, parallel);
    }

    private static VectorCase Load(string id) => id switch
    {
        "V1" => From(
            OfficialTestVectors.V1.Name,
            OfficialTestVectors.V1.MemoryKiB,
            OfficialTestVectors.V1.Iterations,
            OfficialTestVectors.V1.Parallelism,
            OfficialTestVectors.V1.PasswordHex,
            OfficialTestVectors.V1.SaltHex,
            OfficialTestVectors.V1.SeedHex,
            OfficialTestVectors.V1.GroupRootHex,
            OfficialTestVectors.V1.HashHex,
            OfficialTestVectors.V1.Encoded,
            OfficialTestVectors.V1.InitializedBlocks,
            OfficialTestVectors.V1.SampleReferences,
            OfficialTestVectors.V1.ForgeMixSamples),
        "V2" => From(
            OfficialTestVectors.V2.Name,
            OfficialTestVectors.V2.MemoryKiB,
            OfficialTestVectors.V2.Iterations,
            OfficialTestVectors.V2.Parallelism,
            OfficialTestVectors.V2.PasswordHex,
            OfficialTestVectors.V2.SaltHex,
            OfficialTestVectors.V2.SeedHex,
            OfficialTestVectors.V2.GroupRootHex,
            OfficialTestVectors.V2.HashHex,
            OfficialTestVectors.V2.Encoded,
            OfficialTestVectors.V2.InitializedBlocks,
            OfficialTestVectors.V2.SampleReferences,
            OfficialTestVectors.V2.ForgeMixSamples),
        "V3" => From(
            OfficialTestVectors.V3.Name,
            OfficialTestVectors.V3.MemoryKiB,
            OfficialTestVectors.V3.Iterations,
            OfficialTestVectors.V3.Parallelism,
            OfficialTestVectors.V3.PasswordHex,
            OfficialTestVectors.V3.SaltHex,
            OfficialTestVectors.V3.SeedHex,
            OfficialTestVectors.V3.GroupRootHex,
            OfficialTestVectors.V3.HashHex,
            OfficialTestVectors.V3.Encoded,
            OfficialTestVectors.V3.InitializedBlocks,
            OfficialTestVectors.V3.SampleReferences,
            OfficialTestVectors.V3.ForgeMixSamples),
        "V4" => From(
            OfficialTestVectors.V4.Name,
            OfficialTestVectors.V4.MemoryKiB,
            OfficialTestVectors.V4.Iterations,
            OfficialTestVectors.V4.Parallelism,
            OfficialTestVectors.V4.PasswordHex,
            OfficialTestVectors.V4.SaltHex,
            OfficialTestVectors.V4.SeedHex,
            OfficialTestVectors.V4.GroupRootHex,
            OfficialTestVectors.V4.HashHex,
            OfficialTestVectors.V4.Encoded,
            OfficialTestVectors.V4.InitializedBlocks,
            OfficialTestVectors.V4.SampleReferences,
            OfficialTestVectors.V4.ForgeMixSamples),
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    private static VectorCase From(
        string name,
        int memoryKiB,
        int iterations,
        int parallelism,
        string passwordHex,
        string saltHex,
        string seedHex,
        string groupRootHex,
        string hashHex,
        string encoded,
        (int Pass, int Lane, int BlockIndex, string PrefixHex)[] initialized,
        (int Pass, int Lane, int BlockIndex, int PreviousIndex, int ReferenceLane, int ReferenceIndex, bool CrossLane)[] references,
        (int Pass, int Lane, int BlockIndex, string PrefixHex)[] mixSamples)
        => new(
            name,
            new ForgeHashParameters
            {
                MemoryKiB = memoryKiB,
                Iterations = iterations,
                Parallelism = parallelism,
            },
            passwordHex,
            saltHex,
            seedHex,
            groupRootHex,
            hashHex,
            encoded,
            initialized,
            references,
            mixSamples);

    private static string Hex(ReadOnlySpan<byte> data)
        => Convert.ToHexString(data).ToLowerInvariant();

    private sealed record VectorCase(
        string Name,
        ForgeHashParameters Parameters,
        string PasswordHex,
        string SaltHex,
        string SeedHex,
        string GroupRootHex,
        string HashHex,
        string Encoded,
        (int Pass, int Lane, int BlockIndex, string PrefixHex)[] InitializedBlocks,
        (int Pass, int Lane, int BlockIndex, int PreviousIndex, int ReferenceLane, int ReferenceIndex, bool CrossLane)[] SampleReferences,
        (int Pass, int Lane, int BlockIndex, string PrefixHex)[] ForgeMixSamples);
}
