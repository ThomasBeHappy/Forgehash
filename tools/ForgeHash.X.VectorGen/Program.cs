using System.Text;
using System.Text.Json;
using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

// Experimental ForgeHash-X v0 toy vector freezer. Not for production.

string repoRoot = FindRepoRoot();
string outDir = Path.Combine(repoRoot, "implementers", "x0");
string vectorsDir = Path.Combine(outDir, "vectors");
Directory.CreateDirectory(vectorsDir);

var cases = new List<CaseDef>
{
    new(
        "vector1_empty_password_zero_salt",
        [],
        new byte[16],
        ForgeHashXParameters.Toy),
    new(
        "vector2_short_password_incrementing_salt",
        Encoding.UTF8.GetBytes("password"),
        Enumerable.Range(0, 16).Select(i => (byte)i).ToArray(),
        ForgeHashXParameters.Toy),
    new(
        "vector3_two_lanes_toy",
        Encoding.UTF8.GetBytes("x"),
        Enumerable.Repeat((byte)0x42, 16).ToArray(),
        new ForgeHashXParameters
        {
            MemoryKiB = 1024,
            Iterations = 1,
            Parallelism = 2,
            OutputLength = 32,
            SaltLength = 16,
        }),
};

var manifestVectors = new List<object>();
var jsonOpts = new JsonSerializerOptions { WriteIndented = true };

foreach (CaseDef c in cases)
{
    c.Parameters.Validate();
    byte[] seed = ForgeHashXApi.ComputeSeed(c.Password, c.Salt, c.Parameters);
    byte[] hash = ForgeHashXApi.DeriveHash(c.Password, c.Salt, c.Parameters);
    string encoded = ForgeHashXEncoding.Encode(c.Parameters, c.Salt, hash);

    var vector = new
    {
        name = c.Id,
        warning = "Experimental ForgeHash-X v0. Not for production. Not B3-compatible.",
        passwordHex = Convert.ToHexString(c.Password).ToLowerInvariant(),
        saltHex = Convert.ToHexString(c.Salt).ToLowerInvariant(),
        memoryKiB = c.Parameters.MemoryKiB,
        iterations = c.Parameters.Iterations,
        parallelism = c.Parameters.Parallelism,
        outputLength = c.Parameters.OutputLength,
        seedHex = Convert.ToHexString(seed).ToLowerInvariant(),
        hashHex = Convert.ToHexString(hash).ToLowerInvariant(),
        encoded,
    };

    string file = $"vectors/{c.Id}.json";
    File.WriteAllText(Path.Combine(outDir, file), JsonSerializer.Serialize(vector, jsonOpts) + "\n");
    Console.WriteLine($"Wrote {file}");
    Console.WriteLine($"  seed={vector.seedHex}");
    Console.WriteLine($"  hash={vector.hashHex}");
    Console.WriteLine($"  encoded={encoded}");

    manifestVectors.Add(new
    {
        id = c.Id,
        file,
        memoryKiB = c.Parameters.MemoryKiB,
        iterations = c.Parameters.Iterations,
        parallelism = c.Parameters.Parallelism,
        hashHex = vector.hashHex,
        encoded,
    });
}

var manifest = new
{
    algorithm = "ForgeHash-X",
    version = 0,
    warning = "Experimental cryptography. Not for production password storage. Not compatible with ForgeHash-B3. Bit-exact vector match does not imply security.",
    encoding = "$forgehx$v=0$m=<memoryKiB>,t=<iterations>,p=<parallelism>$<salt-b64>$<hash-b64>",
    documentation = new[] { "docs/forgehx/SPECIFICATION_X.md", "docs/forgehx/README.md" },
    vectors = manifestVectors,
};

File.WriteAllText(
    Path.Combine(outDir, "manifest.json"),
    JsonSerializer.Serialize(manifest, jsonOpts) + "\n");
Console.WriteLine("Wrote manifest.json");

static string FindRepoRoot()
{
    string dir = Directory.GetCurrentDirectory();
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir, "ForgeHash.sln")))
        {
            return dir;
        }

        dir = Directory.GetParent(dir)?.FullName!;
    }

    throw new InvalidOperationException("Could not find ForgeHash.sln");
}

file sealed record CaseDef(string Id, byte[] Password, byte[] Salt, ForgeHashXParameters Parameters);
