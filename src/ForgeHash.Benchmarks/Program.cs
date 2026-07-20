using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ForgeHash;
using ForgeHashApi = ForgeHash.ForgeHash;

BenchmarkRunner.Run<ForgeHashBenchmarks>();

/// <summary>
/// BenchmarkDotNet harness covering the specification's scaling sets (§30).
/// Larger sets (256 MiB / 1 GiB) are available via <see cref="LargeSets"/> but
/// excluded from the default job to keep local runs practical.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
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

    /// <summary>Optional heavy sets for dedicated machines.</summary>
    public static IEnumerable<BenchmarkSet> LargeSets =>
    [
        new("256MiB_t4_p2", 262144, 4, 2),
        new("1GiB_t4_p4", 1048576, 4, 4),
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
    {
        var parameters = new ForgeHashParameters
        {
            MemoryKiB = Set.MemoryKiB,
            Iterations = Set.Iterations,
            Parallelism = Set.Parallelism,
        };

        return ForgeHashApi.DeriveHash(_password, _salt, parameters);
    }

    public readonly record struct BenchmarkSet(string Name, int MemoryKiB, int Iterations, int Parallelism)
    {
        public override string ToString() => Name;
    }
}
