using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ForgeHash.ResearchBench;

internal static class BenchCommon
{
    public static PeerResult Time(
        string family,
        string profile,
        string parameters,
        int warmup,
        int samples,
        Action derive)
    {
        for (int i = 0; i < warmup; i++)
        {
            derive();
        }

        var times = new List<double>(samples);
        long peakWs = 0;
        for (int i = 0; i < samples; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sw = Stopwatch.StartNew();
            derive();
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
            peakWs = Math.Max(peakWs, Process.GetCurrentProcess().WorkingSet64);
        }

        times.Sort();
        double median = times[times.Count / 2];
        return new PeerResult(
            Family: family,
            Profile: profile,
            Parameters: parameters,
            MedianMs: median,
            MeanMs: times.Average(),
            MinMs: times[0],
            MaxMs: times[^1],
            OpsPerSec: 1000.0 / median,
            PeakWorkingSetMiB: peakWs / (1024.0 * 1024.0));
    }

    public static void Print(PeerResult r)
    {
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{r.Family,-14} {r.Profile,-28} median={r.MedianMs,8:F2} ms  " +
            $"H/s={r.OpsPerSec,8:F2}  peakWS={r.PeakWorkingSetMiB:F1} MiB"));
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"               {r.Parameters}"));
    }

    public static string BuildPeerMarkdown(string machine, int warmup, int samples, IReadOnlyList<PeerResult> rows)
    {
        var sb = new StringBuilder();
        string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        sb.AppendLine("## Peer algorithm throughput bench");
        sb.AppendLine();
        sb.AppendLine($"**Collected:** {stamp}  ");
        sb.AppendLine($"**Machine:** `{machine}`  ");
        sb.AppendLine($"**Method:** `tools/ForgeHash.ResearchBench --suite peers` — warmup={warmup}, samples={samples}, fixed password/salt where applicable.  ");
        sb.AppendLine("**Caveat:** presets are **common / OWASP-adjacent costs**, not equal work or equal security. Latency ≠ hardness. ForgeHash remains experimental.");
        sb.AppendLine();
        sb.AppendLine("| Family | Profile | Parameters | median ms | H/s | peak WS (MiB) |");
        sb.AppendLine("|---|---|---|---:|---:|---:|");
        foreach (PeerResult r in rows)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {r.Family} | {r.Profile} | `{r.Parameters}` | {r.MedianMs:F2} | {r.OpsPerSec:F2} | {r.PeakWorkingSetMiB:F1} |"));
        }

        sb.AppendLine();
        return sb.ToString();
    }
}

internal sealed record PeerResult(
    string Family,
    string Profile,
    string Parameters,
    double MedianMs,
    double MeanMs,
    double MinMs,
    double MaxMs,
    double OpsPerSec,
    double PeakWorkingSetMiB);

internal static class Args
{
    public static int Int(string[] args, string name, int fallback)
    {
        int i = Array.IndexOf(args, name);
        if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v))
        {
            return v;
        }

        return fallback;
    }

    public static string String(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    public static string? OptionalString(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    public static bool Flag(string[] args, string name) => Array.IndexOf(args, name) >= 0;
}
