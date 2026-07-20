using System.Globalization;
using System.Text;
using System.Text.Json;
using ForgeHash;

/// <summary>
/// One-shot generator for official ForgeHash-B3 v1 test vectors.
/// Usage: dotnet run --project tools/ForgeHash.VectorGen -- <output-directory>
/// </summary>

string outputDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "vectors"));

Directory.CreateDirectory(outputDir);

var cases = new (string Name, byte[] Password, byte[] Salt, ForgeHashParameters Parameters)[]
{
    (
        "vector1_empty_password_zero_salt",
        [],
        new byte[16],
        new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1 }
    ),
    (
        "vector2_password_incrementing_salt",
        "password"u8.ToArray(),
        Enumerable.Range(0, 16).Select(i => (byte)i).ToArray(),
        new ForgeHashParameters { MemoryKiB = 8192, Iterations = 1, Parallelism = 1 }
    ),
    (
        "vector3_utf8_two_lanes",
        Encoding.UTF8.GetBytes("pásswørd-🔐"),
        [
            0x5a, 0x91, 0x2c, 0x7e, 0x11, 0xd4, 0x88, 0x03,
            0xf6, 0x4b, 0x19, 0xae, 0xc0, 0x57, 0x3d, 0x8f
        ],
        new ForgeHashParameters { MemoryKiB = 16384, Iterations = 2, Parallelism = 2 }
    ),
    (
        "vector4_null_bytes_four_lanes",
        [0x00, 0x01, 0x00, 0xff, 0x00],
        Enumerable.Repeat((byte)0x42, 16).ToArray(),
        new ForgeHashParameters { MemoryKiB = 32768, Iterations = 3, Parallelism = 4 }
    ),
};

var snapshots = new List<TestVectorSnapshot>();
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

foreach ((string name, byte[] password, byte[] salt, ForgeHashParameters parameters) in cases)
{
    Console.Error.WriteLine($"Generating {name} (m={parameters.MemoryKiB}, t={parameters.Iterations}, p={parameters.Parallelism})...");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    TestVectorSnapshot snapshot = ForgeHashTestVectors.Generate(name, password, salt, parameters);
    sw.Stop();
    Console.Error.WriteLine($"  done in {sw.Elapsed.TotalSeconds:F2}s  hash={Convert.ToHexString(snapshot.Hash).ToLowerInvariant()}");

    snapshots.Add(snapshot);
    string jsonPath = Path.Combine(outputDir, name + ".json");
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(ToDto(snapshot), jsonOptions));
}

string markdown = BuildMarkdown(snapshots);
await File.WriteAllTextAsync(Path.Combine(outputDir, "FORGEHASH_B3_V1_VECTORS.md"), markdown);

string csharp = BuildCSharp(snapshots);
string csharpPath = Path.GetFullPath(Path.Combine(outputDir, "..", "ForgeHash.Tests", "OfficialTestVectors.g.cs"));
await File.WriteAllTextAsync(csharpPath, csharp);

// Keep the public implementer pack in sync when regenerating the primary vector dir.
string repoRoot = Path.GetFullPath(Path.Combine(outputDir, "..", ".."));
string implementerVectors = Path.Combine(repoRoot, "implementers", "v1", "vectors");
if (Directory.Exists(Path.Combine(repoRoot, "implementers", "v1")))
{
    Directory.CreateDirectory(implementerVectors);
    foreach (string json in Directory.EnumerateFiles(outputDir, "vector*.json"))
    {
        File.Copy(json, Path.Combine(implementerVectors, Path.GetFileName(json)), overwrite: true);
    }

    await File.WriteAllTextAsync(
        Path.Combine(repoRoot, "implementers", "v1", "manifest.json"),
        BuildManifest(snapshots));
    Console.Error.WriteLine($"Synced implementer pack at {Path.Combine(repoRoot, "implementers", "v1")}");
}

Console.Error.WriteLine($"Wrote vectors to {outputDir}");
Console.Error.WriteLine($"Wrote C# pins to {csharpPath}");
return 0;

static object ToDto(TestVectorSnapshot s) => new
{
    s.Name,
    passwordHex = Hex(s.Password),
    saltHex = Hex(s.Salt),
    memoryKiB = s.Parameters.MemoryKiB,
    iterations = s.Parameters.Iterations,
    parallelism = s.Parameters.Parallelism,
    outputLength = s.Parameters.OutputLength,
    seedHex = Hex(s.Seed),
    initializedBlocks = s.InitializedBlocks.Select(b => new
    {
        b.Pass,
        b.Lane,
        b.BlockIndex,
        prefixHex = Hex(b.Prefix),
    }),
    sampleReferences = s.SampleReferences.Select(r => new
    {
        r.Pass,
        r.Slice,
        r.Lane,
        r.BlockIndex,
        r.PreviousIndex,
        r.ReferenceLane,
        r.ReferenceIndex,
        r.CrossLane,
    }),
    forgeMixSamples = s.ForgeMixSamples.Select(b => new
    {
        b.Pass,
        b.Lane,
        b.BlockIndex,
        prefixHex = Hex(b.Prefix),
    }),
    groupRootHex = Hex(s.GroupRoot),
    hashHex = Hex(s.Hash),
    encoded = s.Encoded,
};

static string BuildMarkdown(IReadOnlyList<TestVectorSnapshot> vectors)
{
    var sb = new StringBuilder();
    sb.AppendLine("# ForgeHash-B3 Version 1 Official Test Vectors");
    sb.AppendLine();
    sb.AppendLine("These vectors freeze the ForgeHash-B3 v1 algorithm. Incompatible changes require `v=2`.");
    sb.AppendLine();
    sb.AppendLine("> Experimental cryptography. Not for production password storage.");
    sb.AppendLine();

    int index = 1;
    foreach (TestVectorSnapshot s in vectors)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Vector {index}: `{s.Name}`");
        sb.AppendLine();
        sb.AppendLine("| Field | Value |");
        sb.AppendLine("|-------|-------|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| password_hex | `{Hex(s.Password)}` |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| salt_hex | `{Hex(s.Salt)}` |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| memory_kib | `{s.Parameters.MemoryKiB}` |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| iterations | `{s.Parameters.Iterations}` |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| parallelism | `{s.Parameters.Parallelism}` |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| seed_hex | `{Hex(s.Seed)}` |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| group_root_hex | `{Hex(s.GroupRoot)}` |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| hash_hex | `{Hex(s.Hash)}` |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| encoded | `{s.Encoded}` |");
        sb.AppendLine();

        sb.AppendLine("### Initialized block prefixes (32 bytes)");
        sb.AppendLine();
        foreach (SampledBlock b in s.InitializedBlocks)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- lane {b.Lane} block {b.BlockIndex}: `{Hex(b.Prefix)}`");
        }

        sb.AppendLine();
        sb.AppendLine("### Sample references");
        sb.AppendLine();
        sb.AppendLine("| pass | lane | block | prev | refLane | refBlock | cross |");
        sb.AppendLine("|-----:|-----:|------:|-----:|--------:|---------:|:-----:|");
        foreach (BlockReferenceTrace r in s.SampleReferences)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {r.Pass} | {r.Lane} | {r.BlockIndex} | {r.PreviousIndex} | {r.ReferenceLane} | {r.ReferenceIndex} | {(r.CrossLane ? "yes" : "no")} |");
        }

        sb.AppendLine();
        sb.AppendLine("### ForgeMix sample prefixes (32 bytes)");
        sb.AppendLine();
        foreach (SampledBlock b in s.ForgeMixSamples)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- pass {b.Pass} lane {b.Lane} block {b.BlockIndex}: `{Hex(b.Prefix)}`");
        }

        sb.AppendLine();
        index++;
    }

    return sb.ToString();
}

static string BuildCSharp(IReadOnlyList<TestVectorSnapshot> vectors)
{
    var sb = new StringBuilder();
    sb.AppendLine("// <auto-generated>");
    sb.AppendLine("// Official ForgeHash-B3 v1 test vectors. Regenerated by tools/ForgeHash.VectorGen.");
    sb.AppendLine("// </auto-generated>");
    sb.AppendLine();
    sb.AppendLine("#nullable enable");
    sb.AppendLine("namespace ForgeHash.Tests;");
    sb.AppendLine();
    sb.AppendLine("internal static class OfficialTestVectors");
    sb.AppendLine("{");

    int index = 1;
    foreach (TestVectorSnapshot s in vectors)
    {
        string id = $"V{index}";
        sb.AppendLine(CultureInfo.InvariantCulture, $"    internal static class {id}");
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const string Name = \"{s.Name}\";");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const int MemoryKiB = {s.Parameters.MemoryKiB};");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const int Iterations = {s.Parameters.Iterations};");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const int Parallelism = {s.Parameters.Parallelism};");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const string PasswordHex = \"{Hex(s.Password)}\";");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const string SaltHex = \"{Hex(s.Salt)}\";");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const string SeedHex = \"{Hex(s.Seed)}\";");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const string GroupRootHex = \"{Hex(s.GroupRoot)}\";");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const string HashHex = \"{Hex(s.Hash)}\";");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        internal const string Encoded = \"{s.Encoded}\";");
        sb.AppendLine();
        sb.AppendLine("        internal static readonly (int Pass, int Lane, int BlockIndex, string PrefixHex)[] InitializedBlocks =");
        sb.AppendLine("        [");
        foreach (SampledBlock b in s.InitializedBlocks)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"            ({b.Pass}, {b.Lane}, {b.BlockIndex}, \"{Hex(b.Prefix)}\"),");
        }

        sb.AppendLine("        ];");
        sb.AppendLine();
        sb.AppendLine("        internal static readonly (int Pass, int Lane, int BlockIndex, int PreviousIndex, int ReferenceLane, int ReferenceIndex, bool CrossLane)[] SampleReferences =");
        sb.AppendLine("        [");
        foreach (BlockReferenceTrace r in s.SampleReferences)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"            ({r.Pass}, {r.Lane}, {r.BlockIndex}, {r.PreviousIndex}, {r.ReferenceLane}, {r.ReferenceIndex}, {(r.CrossLane ? "true" : "false")}),");
        }

        sb.AppendLine("        ];");
        sb.AppendLine();
        sb.AppendLine("        internal static readonly (int Pass, int Lane, int BlockIndex, string PrefixHex)[] ForgeMixSamples =");
        sb.AppendLine("        [");
        foreach (SampledBlock b in s.ForgeMixSamples)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"            ({b.Pass}, {b.Lane}, {b.BlockIndex}, \"{Hex(b.Prefix)}\"),");
        }

        sb.AppendLine("        ];");
        sb.AppendLine("    }");
        sb.AppendLine();
        index++;
    }

    sb.AppendLine("}");
    return sb.ToString();
}

static string Hex(ReadOnlySpan<byte> data)
    => Convert.ToHexString(data).ToLowerInvariant();

static string BuildManifest(IReadOnlyList<TestVectorSnapshot> vectors)
{
    var payload = new
    {
        algorithm = "ForgeHash-B3",
        version = 1,
        warning = "Experimental cryptography. Not for production password storage. Bit-exact vector match does not imply security.",
        encoding = "$forgeh$v=1$m=<memoryKiB>,t=<iterations>,p=<parallelism>$<salt-b64>$<hash-b64>",
        documentation = new[]
        {
            "SPECIFICATION.md",
            "docs/IMPLEMENTING.md",
            "docs/USAGE.md",
        },
        vectors = vectors.Select(s => new
        {
            id = s.Name,
            file = $"vectors/{s.Name}.json",
            memoryKiB = s.Parameters.MemoryKiB,
            iterations = s.Parameters.Iterations,
            parallelism = s.Parameters.Parallelism,
            hashHex = Hex(s.Hash),
            encoded = s.Encoded,
        }),
    };

    return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
}
