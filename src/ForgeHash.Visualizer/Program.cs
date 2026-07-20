using System.Globalization;
using System.Text;
using System.Text.Json;
using ForgeHash;
using ForgeHash.Analysis;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

string mode = args[0].ToLowerInvariant();
var options = ParseOptions(args.Skip(1).ToArray());

var parameters = new ForgeHashParameters
{
    MemoryKiB = options.Memory,
    Iterations = options.Iterations,
    Parallelism = options.Parallelism,
};

byte[] password = options.PasswordHex is null
    ? "visualizer"u8.ToArray()
    : Convert.FromHexString(options.PasswordHex);
byte[] salt = options.SaltHex is null
    ? Enumerable.Range(0, 16).Select(i => (byte)i).ToArray()
    : Convert.FromHexString(options.SaltHex);

Console.Error.WriteLine(string.Create(
    CultureInfo.InvariantCulture,
    $"Capturing traces m={parameters.MemoryKiB} t={parameters.Iterations} p={parameters.Parallelism}..."));

ReferenceAnalysis analysis = ReferenceAnalysis.Capture(password, salt, parameters);

switch (mode)
{
    case "json":
    case "report":
        await WriteAsync(options.Output, analysis.ToReportJson());
        break;
    case "csv":
        await WriteAsync(options.Output, analysis.ToCsv());
        break;
    case "heatmap":
        await WriteAsync(options.Output, JsonSerializer.Serialize(analysis.BuildHeatMap(), CreateIndentedJsonOptions()));
        break;
    case "lanes":
        await WriteAsync(options.Output, JsonSerializer.Serialize(analysis.BuildLaneInteraction(), CreateIndentedJsonOptions()));
        break;
    case "passes":
        await WriteAsync(options.Output, JsonSerializer.Serialize(
            analysis.BuildPassHistograms().ToDictionary(
                kv => kv.Key.ToString(CultureInfo.InvariantCulture),
                kv => kv.Value),
            CreateIndentedJsonOptions()));
        break;
    case "dot":
    case "graph":
        await WriteAsync(options.Output, analysis.ToDependencyGraphDot());
        break;
    case "tmto":
        await WriteAsync(options.Output, JsonSerializer.Serialize(analysis.EstimateStandardTmtoLadder(), CreateIndentedJsonOptions()));
        break;
    case "all":
        string dir = options.Output ?? "analysis-out";
        Directory.CreateDirectory(dir);
        JsonSerializerOptions jsonOptions = CreateIndentedJsonOptions();
        await File.WriteAllTextAsync(Path.Combine(dir, "report.json"), analysis.ToReportJson());
        await File.WriteAllTextAsync(Path.Combine(dir, "references.csv"), analysis.ToCsv());
        await File.WriteAllTextAsync(Path.Combine(dir, "heatmap.json"), JsonSerializer.Serialize(analysis.BuildHeatMap(), jsonOptions));
        await File.WriteAllTextAsync(Path.Combine(dir, "lanes.json"), JsonSerializer.Serialize(analysis.BuildLaneInteraction(), jsonOptions));
        await File.WriteAllTextAsync(Path.Combine(dir, "graph.dot"), analysis.ToDependencyGraphDot());
        await File.WriteAllTextAsync(Path.Combine(dir, "tmto.json"), JsonSerializer.Serialize(analysis.EstimateStandardTmtoLadder(), jsonOptions));
        await File.WriteAllTextAsync(Path.Combine(dir, "RESEARCH_NOTES.md"), BuildNotes(analysis));
        Console.WriteLine($"Wrote analysis bundle to {dir}");
        break;
    default:
        PrintUsage();
        return 1;
}

return 0;

static JsonSerializerOptions CreateIndentedJsonOptions()
    => new() { WriteIndented = true };

static async Task WriteAsync(string? path, string content)
{
    if (string.IsNullOrWhiteSpace(path) || path == "-")
    {
        Console.WriteLine(content);
        return;
    }

    await File.WriteAllTextAsync(path, content);
    Console.WriteLine($"Wrote {path}");
}

static void PrintUsage()
{
    Console.Error.WriteLine(
        """
        ForgeHash.Visualizer — research-only reference analysis tool.

        Usage:
          ForgeHash.Visualizer <mode> [--out path] [--memory 8192] [--iterations 1] [--parallelism 1]
                               [--password-hex ...] [--salt-hex ...]

        Modes:
          report|json   Full JSON research report
          csv           Raw reference CSV
          heatmap       Reference heat map JSON
          lanes         Cross-lane interaction JSON
          passes        Per-pass reference histograms
          graph|dot     Dependency graph (Graphviz DOT)
          tmto          Time-memory trade-off estimates
          all           Write a full analysis bundle to --out directory
        """);
}

static Options ParseOptions(string[] args)
{
    var options = new Options();
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--out":
            case "--output":
                options.Output = args[++i];
                break;
            case "--memory":
                options.Memory = int.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--iterations":
                options.Iterations = int.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--parallelism":
                options.Parallelism = int.Parse(args[++i], CultureInfo.InvariantCulture);
                break;
            case "--password-hex":
                options.PasswordHex = args[++i];
                break;
            case "--salt-hex":
                options.SaltHex = args[++i];
                break;
        }
    }

    return options;
}

static string BuildNotes(ReferenceAnalysis analysis)
{
    var sb = new StringBuilder();
    sb.AppendLine("# ForgeHash Research Notes");
    sb.AppendLine();
    sb.AppendLine("Experimental analysis only. Not a security proof.");
    sb.AppendLine();
    sb.AppendLine("## Parameters");
    sb.AppendLine();
    sb.AppendLine(CultureInfo.InvariantCulture, $"- memoryKiB: {analysis.Parameters.MemoryKiB}");
    sb.AppendLine(CultureInfo.InvariantCulture, $"- iterations: {analysis.Parameters.Iterations}");
    sb.AppendLine(CultureInfo.InvariantCulture, $"- parallelism: {analysis.Parameters.Parallelism}");
    sb.AppendLine(CultureInfo.InvariantCulture, $"- hash: `{Convert.ToHexString(analysis.Hash).ToLowerInvariant()}`");
    sb.AppendLine();
    sb.AppendLine("## Cross-lane traffic");
    sb.AppendLine();
    int cross = analysis.Traces.Count(t => t.CrossLane);
    sb.AppendLine(CultureInfo.InvariantCulture, $"- cross-lane references: {cross} / {analysis.Traces.Count}");
    sb.AppendLine();
    sb.AppendLine("## TMTO ladder (retention model)");
    sb.AppendLine();
    sb.AppendLine("| keepEvery | retention | missRate | est. extra ForgeMix factor |");
    sb.AppendLine("|----------:|----------:|---------:|---------------------------:|");
    foreach (TmtoEstimate estimate in analysis.EstimateStandardTmtoLadder())
    {
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| {estimate.KeepEvery} | {estimate.RetentionFraction:P1} | {estimate.ReferenceMissRate:P1} | {estimate.EstimatedExtraMixFactor:F2}x |");
    }

    sb.AppendLine();
    sb.AppendLine("## Interpretation caveats");
    sb.AppendLine();
    sb.AppendLine("- Password-dependent addressing is intentional and creates side-channel risk.");
    sb.AppendLine("- TMTO factors are heuristic from reference traces, not adversarial lower bounds.");
    sb.AppendLine("- GPU/ASIC suitability remains an open research question.");
    return sb.ToString();
}

sealed class Options
{
    public string? Output { get; set; }
    public int Memory { get; set; } = 8192;
    public int Iterations { get; set; } = 1;
    public int Parallelism { get; set; } = 1;
    public string? PasswordHex { get; set; }
    public string? SaltHex { get; set; }
}
