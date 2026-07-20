using System.Globalization;
using System.Text;
using ForgeHash;
using ForgeHash.ResearchBench;
using ForgeHashX;
using ForgeHashApi = ForgeHash.ForgeHash;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

// Research throughput benches.
//   --suite matched   B3 vs X at same (m,t,p)
//   --suite peers     ForgeHash vs bcrypt / Argon2id / scrypt / PBKDF2
//   --suite all       both

string suite = Args.String(args, "--suite", "peers").Trim().ToLowerInvariant();
int warmup = Args.Int(args, "--warmup", 2);
int samples = Args.Int(args, "--samples", 7);
string? outPath = Args.OptionalString(args, "--out");
bool markdown = Args.Flag(args, "--markdown") || outPath is not null;

var machine = $"{Environment.OSVersion} | {Environment.ProcessorCount} logical CPUs | {Environment.Version}";
Console.Error.WriteLine($"ForgeHash research bench — suite={suite} warmup={warmup} samples={samples}");
Console.Error.WriteLine(machine);
Console.Error.WriteLine();

var md = new StringBuilder();
md.AppendLine($"# ForgeHash research bench (`{suite}`)");
md.AppendLine();

if (suite is "matched" or "all")
{
    IReadOnlyList<PeerResult> matched = MatchedSuite.Run(warmup, samples);
    md.Append(BuildMatchedMarkdown(machine, warmup, samples, matched));
}

if (suite is "peers" or "all")
{
    IReadOnlyList<PeerResult> peers = PeerSuite.Run(warmup, samples);
    md.Append(BenchCommon.BuildPeerMarkdown(machine, warmup, samples, peers));
}

if (suite is not ("matched" or "peers" or "all"))
{
    Console.Error.WriteLine("Unknown --suite. Use: matched | peers | all");
    return 2;
}

string text = md.ToString();
if (markdown)
{
    Console.WriteLine(text);
}

if (outPath is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllText(outPath, text, Encoding.UTF8);
    Console.Error.WriteLine($"Wrote {outPath}");
}

return 0;

static string BuildMatchedMarkdown(string machine, int warmup, int samples, IReadOnlyList<PeerResult> rows)
{
    var sb = new StringBuilder();
    string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
    sb.AppendLine("## Matched-cost B3 vs X");
    sb.AppendLine();
    sb.AppendLine($"**Collected:** {stamp}  ");
    sb.AppendLine($"**Machine:** `{machine}`  ");
    sb.AppendLine($"**Method:** warmup={warmup}, samples={samples}. Equal KiB ≠ equal mix work (B3 1024 B blocks vs X 512 B).");
    sb.AppendLine();
    sb.AppendLine("| Profile | Family | Parameters | median ms | H/s |");
    sb.AppendLine("|---|---|---|---:|---:|");
    foreach (PeerResult r in rows)
    {
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"| {r.Profile} | {r.Family} | `{r.Parameters}` | {r.MedianMs:F2} | {r.OpsPerSec:F2} |"));
    }

    sb.AppendLine();
    return sb.ToString();
}

internal static class MatchedSuite
{
    public static IReadOnlyList<PeerResult> Run(int warmup, int samples)
    {
        byte[] password = "research-benchmark-password"u8.ToArray();
        byte[] salt = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
        var rows = new List<PeerResult>();

        void X(string profile, int m, int t, int p)
        {
            PeerResult r = BenchCommon.Time(
                "ForgeHash-X", profile, $"m={m},t={t},p={p}", warmup, samples,
                () => _ = ForgeHashXApi.DeriveHash(password, salt, new ForgeHashXParameters
                {
                    MemoryKiB = m,
                    Iterations = t,
                    Parallelism = p,
                    OutputLength = 32,
                    SaltLength = 16,
                }));
            rows.Add(r);
            BenchCommon.Print(r);
        }

        void B3(string profile, int m, int t, int p)
        {
            PeerResult r = BenchCommon.Time(
                "ForgeHash-B3", profile, $"m={m},t={t},p={p}", warmup, samples,
                () => _ = ForgeHashApi.DeriveHash(password, salt, new ForgeHashParameters
                {
                    MemoryKiB = m,
                    Iterations = t,
                    Parallelism = p,
                }));
            rows.Add(r);
            BenchCommon.Print(r);
        }

        X("X_Toy", 1024, 1, 1);
        X("X_4MiB", 4096, 1, 1);

        foreach (var (name, m, t, p) in new[]
                 {
                     ("Match_8MiB_t1_p1", 8192, 1, 1),
                     ("Match_8MiB_t1_p2", 8192, 1, 2),
                     ("Match_16MiB_t1_p1", 16384, 1, 1),
                     ("Match_16MiB_t2_p1", 16384, 2, 1),
                 })
        {
            X(name, m, t, p);
            B3(name, m, t, p);
            PeerResult xb = rows[^1];
            PeerResult xx = rows[^2];
            if (xx.MedianMs > 0)
            {
                Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"  ratio median(b3)/median(x) = {xb.MedianMs / xx.MedianMs:F2}x"));
            }

            Console.Error.WriteLine();
        }

        return rows;
    }
}
