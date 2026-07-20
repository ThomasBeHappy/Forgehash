namespace ForgeHash.Tests;

public class AvalancheTests
{
    [Fact]
    public void SingleBitFlip_ChangesAboutHalfTheOutputBits()
    {
        byte[] salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var parameters = ForgeHashParameters.Development;

        int totalBits = 0;
        int changedBits = 0;
        const int samples = 2;

        for (int sample = 0; sample < samples; sample++)
        {
            byte[] password = new byte[16];
            Random.Shared.NextBytes(password);

            byte[] baseline = ForgeHash.DeriveHash(password, salt, parameters);

            // Flip a few bits rather than an entire byte to keep runtime practical.
            for (int bit = 0; bit < 2; bit++)
            {
                password[0] ^= (byte)(1 << bit);
                byte[] flipped = ForgeHash.DeriveHash(password, salt, parameters);
                password[0] ^= (byte)(1 << bit);

                for (int i = 0; i < baseline.Length; i++)
                {
                    byte diff = (byte)(baseline[i] ^ flipped[i]);
                    changedBits += System.Numerics.BitOperations.PopCount(diff);
                    totalBits += 8;
                }
            }
        }

        double ratio = changedBits / (double)totalBits;
        Assert.InRange(ratio, 0.35, 0.65);
    }
}
