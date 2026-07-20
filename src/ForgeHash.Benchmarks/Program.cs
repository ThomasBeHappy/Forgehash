using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using ForgeHash;
using ForgeHashX;
using ForgeHashApi = ForgeHash.ForgeHash;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

// Default: matched-cost research job (ShortRun).
// Full B3 scaling matrix: --filter *ForgeHashBenchmarks*
//   dotnet run -c Release --project src/ForgeHash.Benchmarks -- --filter *MatchedCost*

var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun.WithId("ResearchShort"));

BenchmarkSwitcher.FromAssembly(typeof(MatchedCostBenchmarks).Assembly).Run(args, config);

/// <summary>
/// Matched nominal cost: same m / t / p for B3 and X (m ≥ B3 minimum 8192 KiB).
/// Equal KiB is not equal mix work (B3 1024-byte blocks vs X 512-byte blocks).
/// </summary>
[MemoryDiagnoser]
public class MatchedCostBenchmarks
{
    private byte[] _password = null!;
    private byte[] _salt = null!;

    [ParamsSource(nameof(Sets))]
    public CostSet Set { get; set; }

    public static IEnumerable<CostSet> Sets =>
    [
        new("8MiB_t1_p1", 8192, 1, 1),
        new("8MiB_t1_p2", 8192, 1, 2),
        new("16MiB_t1_p1", 16384, 1, 1),
    ];

    [GlobalSetup]
    public void Setup()
    {
        _password = "benchmark-password"u8.ToArray();
        _salt = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
    }

    [Benchmark(Baseline = true)]
    public byte[] B3_DeriveHash()
        => ForgeHashApi.DeriveHash(_password, _salt, new ForgeHashParameters
        {
            MemoryKiB = Set.MemoryKiB,
            Iterations = Set.Iterations,
            Parallelism = Set.Parallelism,
        });

    [Benchmark]
    public byte[] X_DeriveHash()
        => ForgeHashXApi.DeriveHash(_password, _salt, new ForgeHashXParameters
        {
            MemoryKiB = Set.MemoryKiB,
            Iterations = Set.Iterations,
            Parallelism = Set.Parallelism,
            OutputLength = 32,
            SaltLength = 16,
        });

    public readonly record struct CostSet(string Name, int MemoryKiB, int Iterations, int Parallelism)
    {
        public override string ToString() => Name;
    }
}

/// <summary>Original B3-only scaling harness (spec §30-ish). Opt-in via filter.</summary>
[MemoryDiagnoser]
public class ForgeHashBenchmarks
{
    private byte[] _password = null!;
    private byte[] _salt = null!;

    [ParamsSource(nameof(DefaultSets))]
    public BenchmarkSet Set { get; set; }

    public static IEnumerable<BenchmarkSet> DefaultSets =>
    [
        new("8MiB_t1_p1", 8192, 1, 1),
        new("16MiB_t2_p1", 16384, 2, 1),
        new("64MiB_t3_p1", 65536, 3, 1),
        new("64MiB_t3_p2", 65536, 3, 2),
    ];

    [GlobalSetup]
    public void Setup()
    {
        _password = "benchmark-password"u8.ToArray();
        _salt = new byte[16];
        Random.Shared.NextBytes(_salt);
    }

    [Benchmark]
    public byte[] DeriveHash()
        => ForgeHashApi.DeriveHash(_password, _salt, new ForgeHashParameters
        {
            MemoryKiB = Set.MemoryKiB,
            Iterations = Set.Iterations,
            Parallelism = Set.Parallelism,
        });

    public readonly record struct BenchmarkSet(string Name, int MemoryKiB, int Iterations, int Parallelism)
    {
        public override string ToString() => Name;
    }
}
