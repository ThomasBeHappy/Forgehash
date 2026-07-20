using ForgeHash.Internal;

namespace ForgeHash.Tests;

public class VersionMetadataTests
{
    [Fact]
    public void Seed_DependsOnVersionMetadataInEncodedInput()
    {
        byte[] password = "version"u8.ToArray();
        byte[] salt = new byte[16];
        var parameters = ForgeHashParameters.Development;

        byte[] v1Input = BinaryEncoding.BuildEncodedInput(
            version: 1,
            parameters.MemoryKiB,
            parameters.Iterations,
            parameters.Parallelism,
            parameters.OutputLength,
            password,
            salt,
            ReadOnlySpan<byte>.Empty);

        byte[] v2Input = BinaryEncoding.BuildEncodedInput(
            version: 2,
            parameters.MemoryKiB,
            parameters.Iterations,
            parameters.Parallelism,
            parameters.OutputLength,
            password,
            salt,
            ReadOnlySpan<byte>.Empty);

        byte[] seedV1 = Blake3Adapter.DeriveSeed(v1Input);
        byte[] seedV2 = Blake3Adapter.DeriveSeed(v2Input);

        Assert.NotEqual(seedV1, seedV2);
        Assert.Equal(seedV1, ForgeHash.ComputeSeed(password, salt, parameters));
    }
}
