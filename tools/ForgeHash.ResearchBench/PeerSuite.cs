using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using ForgeHash;
using ForgeHashX;
using Konscious.Security.Cryptography;
using Scrypt;
using ForgeHashApi = ForgeHash.ForgeHash;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

namespace ForgeHash.ResearchBench;

/// <summary>
/// Wall-clock comparison against common password KDFs at documented presets.
/// Not an equal-work or equal-security claim.
/// </summary>
internal static class PeerSuite
{
    public static IReadOnlyList<PeerResult> Run(int warmup, int samples)
    {
        byte[] password = "research-benchmark-password"u8.ToArray();
        string passwordText = Encoding.UTF8.GetString(password);
        byte[] salt16 = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
        // bcrypt encodes its own salt; we still hash the same password string.
        var rows = new List<PeerResult>();

        void Add(string family, string profile, string parameters, Action derive)
        {
            PeerResult r = BenchCommon.Time(family, profile, parameters, warmup, samples, derive);
            rows.Add(r);
            BenchCommon.Print(r);
            Console.Error.WriteLine();
        }

        // —— ForgeHash ——
        Add("ForgeHash-B3", "Development", "m=8192 KiB, t=1, p=1",
            () => _ = ForgeHashApi.DeriveHash(password, salt16, new ForgeHashParameters
            {
                MemoryKiB = 8192,
                Iterations = 1,
                Parallelism = 1,
            }));

        Add("ForgeHash-B3", "Interactive", "m=65536 KiB, t=3, p=1",
            () => _ = ForgeHashApi.DeriveHash(password, salt16, ForgeHashParameters.Interactive));

        Add("ForgeHash-X", "Toy (sandbox)", "m=1024 KiB, t=1, p=1",
            () => _ = ForgeHashXApi.DeriveHash(password, salt16, ForgeHashXParameters.Toy));

        Add("ForgeHash-X", "Match-8MiB", "m=8192 KiB, t=1, p=1",
            () => _ = ForgeHashXApi.DeriveHash(password, salt16, new ForgeHashXParameters
            {
                MemoryKiB = 8192,
                Iterations = 1,
                Parallelism = 1,
                OutputLength = 32,
                SaltLength = 16,
            }));

        Add("ForgeHash-X", "Match-64MiB_t3", "m=65536 KiB, t=3, p=1",
            () => _ = ForgeHashXApi.DeriveHash(password, salt16, new ForgeHashXParameters
            {
                MemoryKiB = 65536,
                Iterations = 3,
                Parallelism = 1,
                OutputLength = 32,
                SaltLength = 16,
            }));

        // —— Argon2id (Konscious) ——
        // OWASP minimum-ish: 19 MiB, t=2, p=1
        Add("Argon2id", "OWASP-min-ish", "m=19456 KiB (19 MiB), t=2, p=1, out=32",
            () => _ = HashArgon2id(password, salt16, memoryKiB: 19456, iterations: 2, parallelism: 1));

        // Stronger “interactive” profile often cited (~64 MiB)
        Add("Argon2id", "64MiB_t3_p1", "m=65536 KiB (64 MiB), t=3, p=1, out=32",
            () => _ = HashArgon2id(password, salt16, memoryKiB: 65536, iterations: 3, parallelism: 1));

        // —— bcrypt ——
        Add("bcrypt", "cost-10", "work factor 10 (OWASP legacy minimum)",
            () => _ = BCrypt.Net.BCrypt.HashPassword(passwordText, workFactor: 10));

        Add("bcrypt", "cost-12", "work factor 12 (common interactive)",
            () => _ = BCrypt.Net.BCrypt.HashPassword(passwordText, workFactor: 12));

        // —— scrypt (Scrypt.NET) ——
        // OWASP mentions N=2^17 as a minimum CPU/memory cost; also include lighter N=2^14.
        Add("scrypt", "N=2^14_r8_p1", "N=16384, r=8, p=1 (~16 MiB class)",
            () => _ = HashScrypt(passwordText, logN: 14, r: 8, p: 1));

        Add("scrypt", "N=2^17_r8_p1", "N=131072, r=8, p=1 (OWASP-min-ish)",
            () => _ = HashScrypt(passwordText, logN: 17, r: 8, p: 1));


        // —— PBKDF2 ——
        Add("PBKDF2-SHA256", "OWASP-600k", "iterations=600000, salt=16, out=32",
            () => _ = Rfc2898DeriveBytes.Pbkdf2(
                password, salt16, 600_000, HashAlgorithmName.SHA256, 32));

        Add("PBKDF2-SHA256", "iter-310k", "iterations=310000, salt=16, out=32",
            () => _ = Rfc2898DeriveBytes.Pbkdf2(
                password, salt16, 310_000, HashAlgorithmName.SHA256, 32));

        return rows;
    }

    private static byte[] HashArgon2id(byte[] password, byte[] salt, int memoryKiB, int iterations, int parallelism)
    {
        using var argon = new Argon2id(password)
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            MemorySize = memoryKiB, // KiB in Konscious
            Iterations = iterations,
        };
        return argon.GetBytes(32);
    }

    private static string HashScrypt(string password, int logN, int r, int p)
    {
        // ScryptEncoder(iterationCount, blockSize, threadCount) where iterationCount = N = 2^logN.
        var encoder = new ScryptEncoder(1 << logN, r, p);
        return encoder.Encode(password);
    }
}

