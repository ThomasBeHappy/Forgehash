using ForgeHash;
using ForgeHashApi = global::ForgeHash.ForgeHash;

namespace ForgeHash.CrossImplementation.Tests;

/// <summary>
/// Placeholder suite for comparing independent ForgeHash implementations against
/// the frozen official v1 vectors. Add alternate language ports as they appear.
/// </summary>
public class OfficialVectorInteropTests
{
    [Fact]
    public void DotNetReference_MatchesFrozenVector2()
    {
        // Until a second implementation exists, this suite pins the .NET reference
        // against the published vector file contract.
        byte[] password = Convert.FromHexString("70617373776f7264");
        byte[] salt = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = 8192,
            Iterations = 1,
            Parallelism = 1,
        };

        byte[] hash = ForgeHashApi.DeriveHash(password, salt, parameters);
        Assert.Equal(
            "02acdfa7faa0f149fe700b2f46b792fda8eaecd5f14844142c67709c561a6a98",
            Convert.ToHexString(hash).ToLowerInvariant());
    }
}
