using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ForgeHash.Analysis;

/// <summary>
/// Collects block-reference traces and produces research artifacts (§31 / §32).
/// </summary>
public sealed class ReferenceAnalysis
{
    private readonly List<BlockReferenceTrace> _traces = [];
    private readonly ForgeHashParameters _parameters;
    private readonly byte[] _hash;

    private ReferenceAnalysis(ForgeHashParameters parameters, byte[] hash, List<BlockReferenceTrace> traces)
    {
        _parameters = parameters;
        _hash = hash;
        _traces = traces;
    }

    /// <summary>
    /// Runs ForgeHash with tracing enabled and returns an analysis object.
    /// </summary>
    public static ReferenceAnalysis Capture(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ForgeHashParameters parameters)
    {
        var traces = new List<BlockReferenceTrace>();
        byte[] hash = ForgeHash.DeriveHashWithTrace(password, salt, parameters, traces.Add);
        return new ReferenceAnalysis(parameters, hash, traces);
    }

    public IReadOnlyList<BlockReferenceTrace> Traces => _traces;

    public byte[] Hash => _hash;

    public ForgeHashParameters Parameters => _parameters;

    public Dictionary<string, int> BuildHeatMap()
    {
        var heat = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (BlockReferenceTrace trace in _traces)
        {
            string key = $"{trace.ReferenceLane}:{trace.ReferenceIndex}";
            heat[key] = heat.GetValueOrDefault(key) + 1;
        }

        return heat;
    }

    public IReadOnlyList<LaneInteraction> BuildLaneInteraction()
    {
        var map = new Dictionary<(int Pass, int From, int To), int>();
        foreach (BlockReferenceTrace trace in _traces.Where(t => t.CrossLane))
        {
            var key = (trace.Pass, trace.Lane, trace.ReferenceLane);
            map[key] = map.GetValueOrDefault(key) + 1;
        }

        return map
            .OrderBy(kv => kv.Key.Pass)
            .ThenBy(kv => kv.Key.From)
            .ThenBy(kv => kv.Key.To)
            .Select(kv => new LaneInteraction(kv.Key.Pass, kv.Key.From, kv.Key.To, kv.Value))
            .ToList();
    }

    public IReadOnlyDictionary<int, int[]> BuildPassHistograms()
    {
        int blocksPerLane = _parameters.MemoryKiB / _parameters.Parallelism;
        var result = new Dictionary<int, int[]>();

        foreach (IGrouping<int, BlockReferenceTrace> group in _traces.GroupBy(t => t.Pass))
        {
            int[] hist = new int[blocksPerLane];
            foreach (BlockReferenceTrace trace in group)
            {
                if ((uint)trace.ReferenceIndex < (uint)hist.Length)
                {
                    hist[trace.ReferenceIndex]++;
                }
            }

            result[group.Key] = hist;
        }

        return result;
    }

    /// <summary>
    /// Estimates recomputation pressure if only every Nth block is retained.
    /// </summary>
    public TmtoEstimate EstimateTmto(int keepEvery)
    {
        if (keepEvery < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(keepEvery));
        }

        int blocksPerLane = _parameters.MemoryKiB / _parameters.Parallelism;
        int retained = 0;
        int missingHits = 0;
        int totalRefs = _traces.Count;

        for (int lane = 0; lane < _parameters.Parallelism; lane++)
        {
            for (int block = 0; block < blocksPerLane; block++)
            {
                if (block % keepEvery == 0)
                {
                    retained++;
                }
            }
        }

        foreach (BlockReferenceTrace trace in _traces)
        {
            if (trace.ReferenceIndex % keepEvery != 0)
            {
                missingHits++;
            }
        }

        double retention = retained / (double)(_parameters.MemoryKiB);
        double missRate = totalRefs == 0 ? 0 : missingHits / (double)totalRefs;

        // Rough cost model: each miss forces recomputing a dependency chain of length ~keepEvery/2.
        double estimatedExtraMixFactor = 1.0 + (missRate * (keepEvery / 2.0));

        return new TmtoEstimate(
            KeepEvery: keepEvery,
            RetentionFraction: retention,
            ReferenceMissRate: missRate,
            EstimatedExtraMixFactor: estimatedExtraMixFactor,
            RetainedBlocks: retained,
            MissingReferenceHits: missingHits,
            TotalReferences: totalRefs);
    }

    public IReadOnlyList<TmtoEstimate> EstimateStandardTmtoLadder()
    {
        // 75%, 50%, 25%, 12.5% retention ≈ keep every 1.33/~2/4/8 — use integer strides.
        return
        [
            EstimateTmto(1),  // 100%
            EstimateTmto(2),  // ~50%
            EstimateTmto(4),  // ~25%
            EstimateTmto(8),  // ~12.5%
        ];
    }

    public string ToDependencyGraphDot(int maxEdges = 400)
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph ForgeHashRefs {");
        sb.AppendLine("  rankdir=LR;");
        sb.AppendLine("  node [shape=box, fontsize=9];");

        foreach (BlockReferenceTrace trace in _traces.Take(maxEdges))
        {
            string from = $"L{trace.Lane}B{trace.PreviousIndex}";
            string self = $"L{trace.Lane}B{trace.BlockIndex}";
            string reference = $"L{trace.ReferenceLane}B{trace.ReferenceIndex}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"  \"{from}\" -> \"{self}\" [color=gray];");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  \"{reference}\" -> \"{self}\" [color={(trace.CrossLane ? "red" : "blue")}];");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("pass,slice,lane,blockIndex,previousIndex,referenceLane,referenceIndex,crossLane");
        foreach (BlockReferenceTrace t in _traces)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{t.Pass},{t.Slice},{t.Lane},{t.BlockIndex},{t.PreviousIndex},{t.ReferenceLane},{t.ReferenceIndex},{(t.CrossLane ? 1 : 0)}");
        }

        return sb.ToString();
    }

    public object ToReportObject()
    {
        Dictionary<string, int> heat = BuildHeatMap();
        return new
        {
            warning = "Experimental research report. Not a security proof.",
            parameters = new
            {
                _parameters.MemoryKiB,
                _parameters.Iterations,
                _parameters.Parallelism,
            },
            hashHex = Convert.ToHexString(_hash).ToLowerInvariant(),
            totalReferences = _traces.Count,
            crossLaneCount = _traces.Count(t => t.CrossLane),
            crossLaneRate = _traces.Count == 0 ? 0 : _traces.Count(t => t.CrossLane) / (double)_traces.Count,
            heatMapTop = heat.OrderByDescending(kv => kv.Value).Take(32)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            laneInteraction = BuildLaneInteraction(),
            tmto = EstimateStandardTmtoLadder(),
            passCoverage = BuildPassHistograms().ToDictionary(
                kv => kv.Key.ToString(CultureInfo.InvariantCulture),
                kv => new
                {
                    referencedBlocks = kv.Value.Count(c => c > 0),
                    maxHits = kv.Value.Length == 0 ? 0 : kv.Value.Max(),
                    earlyShare = kv.Value.Length == 0
                        ? 0
                        : kv.Value.Take(kv.Value.Length / 4).Sum() / (double)Math.Max(1, kv.Value.Sum()),
                }),
        };
    }

    public string ToReportJson()
        => JsonSerializer.Serialize(ToReportObject(), new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Result of a simple retention-based TMTO estimate.
/// </summary>
public sealed record TmtoEstimate(
    int KeepEvery,
    double RetentionFraction,
    double ReferenceMissRate,
    double EstimatedExtraMixFactor,
    int RetainedBlocks,
    int MissingReferenceHits,
    int TotalReferences);

/// <summary>
/// Aggregated cross-lane reference counts for one pass.
/// </summary>
public sealed record LaneInteraction(int Pass, int FromLane, int ToLane, int Count);
