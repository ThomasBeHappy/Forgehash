using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeHash.Analysis;

/// <summary>Empirical collision / uniqueness campaign kinds.</summary>
public enum CollisionCampaignKind
{
    DistinctPasswords,
    DistinctSalts,
    RandomPairs,
    NearbyPasswordBitFlips,
    DistinctParameterSets,
    TruncatedOutputs,
}

/// <summary>Why a campaign stopped.</summary>
public enum CollisionStopReason
{
    Completed,
    Cancelled,
    StoppedOnFirstCollision,
}

/// <summary>Live progress for UI / logging.</summary>
public sealed record CollisionProgress(
    int Completed,
    int Total,
    int CollisionCount,
    double HashesPerSecond,
    TimeSpan Elapsed,
    string? LastDigestHex,
    string? LatestMessage);

/// <summary>One recorded collision (hash or seed).</summary>
public sealed record CollisionHit(
    int SampleIndex,
    int PriorSampleIndex,
    string DigestHex,
    string Channel,
    string PasswordHex,
    string SaltHex,
    string? ParametersSummary);

/// <summary>Final campaign outcome.</summary>
public sealed class CollisionCampaignResult
{
    public required CollisionCampaignKind Kind { get; init; }
    public required ForgeHashParameters Parameters { get; init; }
    public required int RequestedSamples { get; init; }
    public required int CompletedSamples { get; init; }
    public required int UniqueDigests { get; init; }
    public required int CollisionCount { get; init; }
    public required IReadOnlyList<CollisionHit> Collisions { get; init; }
    public required double HashesPerSecond { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public required CollisionStopReason StopReason { get; init; }
    public int? RngSeed { get; init; }
    public int DegreeOfParallelism { get; init; }

    public string ToJson()
    {
        var payload = new
        {
            kind = Kind.ToString(),
            parameters = new
            {
                memoryKiB = Parameters.MemoryKiB,
                iterations = Parameters.Iterations,
                parallelism = Parameters.Parallelism,
                outputLength = Parameters.OutputLength,
                saltLength = Parameters.SaltLength,
            },
            requestedSamples = RequestedSamples,
            completedSamples = CompletedSamples,
            uniqueDigests = UniqueDigests,
            collisionCount = CollisionCount,
            hashesPerSecond = HashesPerSecond,
            elapsedSeconds = Elapsed.TotalSeconds,
            stopReason = StopReason.ToString(),
            rngSeed = RngSeed,
            degreeOfParallelism = DegreeOfParallelism,
            collisions = Collisions.Select(c => new
            {
                sampleIndex = c.SampleIndex,
                priorSampleIndex = c.PriorSampleIndex,
                digestHex = c.DigestHex,
                channel = c.Channel,
                passwordHex = c.PasswordHex,
                saltHex = c.SaltHex,
                parameters = c.ParametersSummary,
            }),
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
    }

    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("sampleIndex,priorSampleIndex,channel,digestHex,passwordHex,saltHex,parameters");
        foreach (CollisionHit hit in Collisions)
        {
            sb.Append(hit.SampleIndex).Append(',')
                .Append(hit.PriorSampleIndex).Append(',')
                .Append(Csv(hit.Channel)).Append(',')
                .Append(Csv(hit.DigestHex)).Append(',')
                .Append(Csv(hit.PasswordHex)).Append(',')
                .Append(Csv(hit.SaltHex)).Append(',')
                .Append(Csv(hit.ParametersSummary ?? string.Empty))
                .AppendLine();
        }

        return sb.ToString();
    }

    private static string Csv(string value)
        => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}

/// <summary>
/// Mass collision / uniqueness hunts for ForgeHash-B3 (empirical; not a security proof).
/// Samples are hashed concurrently across worker threads.
/// </summary>
public static class CollisionCampaign
{
    /// <summary>
    /// Suggests a worker count from CPU cores, capped so concurrent memory matrices stay reasonable.
    /// </summary>
    public static int SuggestDegreeOfParallelism(ForgeHashParameters parameters)
    {
        int cpu = Math.Max(1, Environment.ProcessorCount);
        // Each in-flight DeriveHash holds ~MemoryKiB KiB. Aim for ≤ ~1.5 GiB of matrices.
        long budgetBytes = 1536L * 1024 * 1024;
        long perHash = Math.Max(1, (long)parameters.MemoryKiB * 1024);
        int byMemory = (int)Math.Max(1, budgetBytes / perHash);
        return Math.Clamp(Math.Min(cpu, byMemory), 1, 64);
    }

    public static CollisionCampaignResult Run(
        CollisionCampaignKind kind,
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirstCollision = false,
        int? rngSeed = null,
        int? maxDegreeOfParallelism = null,
        IProgress<CollisionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleCount, 1);
        int dop = maxDegreeOfParallelism ?? SuggestDegreeOfParallelism(parameters);
        ArgumentOutOfRangeException.ThrowIfLessThan(dop, 1);

        return kind switch
        {
            CollisionCampaignKind.DistinctPasswords => RunDistinctPasswords(
                sampleCount, parameters, stopOnFirstCollision, dop, progress, cancellationToken),
            CollisionCampaignKind.DistinctSalts => RunDistinctSalts(
                sampleCount, parameters, stopOnFirstCollision, rngSeed, dop, progress, cancellationToken),
            CollisionCampaignKind.RandomPairs => RunRandomPairs(
                sampleCount, parameters, stopOnFirstCollision, rngSeed, dop, progress, cancellationToken),
            CollisionCampaignKind.NearbyPasswordBitFlips => RunNearbyBitFlips(
                sampleCount, parameters, stopOnFirstCollision, dop, progress, cancellationToken),
            CollisionCampaignKind.DistinctParameterSets => RunDistinctParameterSets(
                stopOnFirstCollision, dop, progress, cancellationToken),
            CollisionCampaignKind.TruncatedOutputs => RunTruncatedOutputs(
                sampleCount, parameters, stopOnFirstCollision, dop, progress, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static CollisionCampaignResult RunDistinctPasswords(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        int dop,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        int saltLen = Math.Clamp(parameters.SaltLength, 16, 64);
        byte[] salt = new byte[saltLen];
        for (int i = 0; i < saltLen; i++)
        {
            salt[i] = (byte)i;
        }

        SampleJob[] jobs = new SampleJob[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            jobs[i] = new SampleJob(i, BitConverter.GetBytes(i), salt, parameters, TrackSeed: true);
        }

        return RunJobs(CollisionCampaignKind.DistinctPasswords, jobs, parameters, null, stopOnFirst, dop, progress, ct);
    }

    private static CollisionCampaignResult RunDistinctSalts(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        int? rngSeed,
        int dop,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        byte[] password = "collision-salt"u8.ToArray();
        int saltLen = Math.Clamp(parameters.SaltLength, 16, 64);
        Random rng = CreateRng(rngSeed);
        SampleJob[] jobs = new SampleJob[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            byte[] salt = new byte[saltLen];
            BinaryPrimitives.WriteInt32LittleEndian(salt, i);
            rng.NextBytes(salt.AsSpan(4));
            jobs[i] = new SampleJob(i, password, salt, parameters, TrackSeed: true);
        }

        return RunJobs(CollisionCampaignKind.DistinctSalts, jobs, parameters, rngSeed, stopOnFirst, dop, progress, ct);
    }

    private static CollisionCampaignResult RunRandomPairs(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        int? rngSeed,
        int dop,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        int saltLen = Math.Clamp(parameters.SaltLength, 16, 64);
        Random rng = CreateRng(rngSeed);
        SampleJob[] jobs = new SampleJob[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            byte[] password = new byte[16];
            byte[] salt = new byte[saltLen];
            BinaryPrimitives.WriteInt32LittleEndian(password, i);
            BinaryPrimitives.WriteInt32LittleEndian(salt, i ^ 0x5a5a5a5a);
            rng.NextBytes(password.AsSpan(4));
            rng.NextBytes(salt.AsSpan(4));
            jobs[i] = new SampleJob(i, password, salt, parameters, TrackSeed: false);
        }

        return RunJobs(CollisionCampaignKind.RandomPairs, jobs, parameters, rngSeed, stopOnFirst, dop, progress, ct);
    }

    private static CollisionCampaignResult RunNearbyBitFlips(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        int dop,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        int bitCount = sampleCount;
        int passwordLen = Math.Max(1, (bitCount + 7) / 8);
        byte[] basePassword = new byte[passwordLen];
        for (int i = 0; i < passwordLen; i++)
        {
            basePassword[i] = (byte)(0x21 + (i % 40));
        }

        byte[] salt = new byte[Math.Clamp(parameters.SaltLength, 16, 64)];
        salt.AsSpan().Fill(0x11);

        var state = new HuntState(CollisionCampaignKind.NearbyPasswordBitFlips, bitCount, parameters, null, dop);
        byte[] baseline = Derive(basePassword, salt, parameters, sampleDop: dop);
        state.Observe(-1, baseline, "hash", basePassword, salt, null, null);
        state.Report(progress, "baseline");

        SampleJob[] jobs = new SampleJob[bitCount];
        for (int bitIndex = 0; bitIndex < bitCount; bitIndex++)
        {
            byte[] flipped = (byte[])basePassword.Clone();
            flipped[bitIndex / 8] ^= (byte)(1 << (bitIndex % 8));
            jobs[bitIndex] = new SampleJob(bitIndex, flipped, salt, parameters, TrackSeed: false);
        }

        return RunJobs(state, jobs, stopOnFirst, progress, ct);
    }

    private static CollisionCampaignResult RunDistinctParameterSets(
        bool stopOnFirst,
        int dop,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        byte[] password = "params"u8.ToArray();
        byte[] salt = new byte[16];
        ForgeHashParameters[] sets =
        [
            new() { MemoryKiB = 8192, Iterations = 1, Parallelism = 1, OutputLength = 32 },
            new() { MemoryKiB = 16384, Iterations = 1, Parallelism = 1, OutputLength = 32 },
            new() { MemoryKiB = 8192, Iterations = 2, Parallelism = 1, OutputLength = 32 },
            new() { MemoryKiB = 8192, Iterations = 1, Parallelism = 2, OutputLength = 32 },
            new() { MemoryKiB = 8192, Iterations = 1, Parallelism = 1, OutputLength = 16 },
            new() { MemoryKiB = 8192, Iterations = 1, Parallelism = 1, OutputLength = 48 },
        ];

        SampleJob[] jobs = new SampleJob[sets.Length];
        for (int i = 0; i < sets.Length; i++)
        {
            ForgeHashParameters p = sets[i];
            string summary = $"m={p.MemoryKiB},t={p.Iterations},p={p.Parallelism},out={p.OutputLength}";
            jobs[i] = new SampleJob(i, password, salt, p, TrackSeed: false, Channel: "hash-prefix16", ComparePrefix: 16, ParametersSummary: summary);
        }

        return RunJobs(CollisionCampaignKind.DistinctParameterSets, jobs, sets[0], null, stopOnFirst, dop, progress, ct);
    }

    private static CollisionCampaignResult RunTruncatedOutputs(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        int dop,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        var truncated = parameters with { OutputLength = 16 };
        byte[] salt = new byte[Math.Clamp(truncated.SaltLength, 16, 64)];
        SampleJob[] jobs = new SampleJob[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            byte[] password = [(byte)i, (byte)(i >> 8), 0xAA, 0xBB];
            jobs[i] = new SampleJob(i, password, salt, truncated, TrackSeed: false);
        }

        return RunJobs(CollisionCampaignKind.TruncatedOutputs, jobs, truncated, null, stopOnFirst, dop, progress, ct);
    }

    private static CollisionCampaignResult RunJobs(
        CollisionCampaignKind kind,
        SampleJob[] jobs,
        ForgeHashParameters parameters,
        int? rngSeed,
        bool stopOnFirst,
        int dop,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        var state = new HuntState(kind, jobs.Length, parameters, rngSeed, dop);
        return RunJobs(state, jobs, stopOnFirst, progress, ct);
    }

    private static CollisionCampaignResult RunJobs(
        HuntState state,
        SampleJob[] jobs,
        bool stopOnFirst,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = state.DegreeOfParallelism,
            CancellationToken = stopCts.Token,
        };

        try
        {
            Parallel.ForEach(jobs, options, (job, loop) =>
            {
                if (stopCts.IsCancellationRequested)
                {
                    loop.Stop();
                    return;
                }

                // When many samples run in parallel, keep each hash single-threaded on lanes
                // to avoid oversubscription. Lane-parallel is used only for DOP == 1.
                byte[] hash = Derive(job.Password, job.Salt, job.Parameters, state.DegreeOfParallelism);
                state.Observe(job.SampleIndex, hash, job.Channel, job.Password, job.Salt, job.ParametersSummary, job.ComparePrefix);

                if (job.TrackSeed)
                {
                    byte[] seed = ForgeHash.ComputeSeed(job.Password, job.Salt, job.Parameters);
                    state.Observe(job.SampleIndex, seed, "seed", job.Password, job.Salt, null, null);
                }

                state.BumpCompleted();
                state.ReportThrottled(progress, null);

                if (stopOnFirst && state.CollisionCount > 0)
                {
                    stopCts.Cancel();
                    loop.Stop();
                }
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // stop-on-first path
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException))
        {
            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }
            // stop-on-first path
        }

        CollisionStopReason reason =
            ct.IsCancellationRequested ? CollisionStopReason.Cancelled
            : stopOnFirst && state.CollisionCount > 0 ? CollisionStopReason.StoppedOnFirstCollision
            : CollisionStopReason.Completed;

        if (reason == CollisionStopReason.Cancelled)
        {
            throw new OperationCanceledException(ct);
        }

        state.Report(progress, reason.ToString());
        return state.Build(reason);
    }

    private static byte[] Derive(byte[] password, byte[] salt, ForgeHashParameters parameters, int sampleDop)
    {
        // Only use in-hash lane parallelism when we are not already spreading samples across cores.
        if (sampleDop <= 1 && parameters.Parallelism > 1)
        {
            return ForgeHash.DeriveHashParallel(password, salt, parameters);
        }

        return ForgeHash.DeriveHash(password, salt, parameters);
    }

    private static Random CreateRng(int? seed)
        => seed is null ? Random.Shared : new Random(seed.Value);

    private sealed record SampleJob(
        int SampleIndex,
        byte[] Password,
        byte[] Salt,
        ForgeHashParameters Parameters,
        bool TrackSeed,
        string Channel = "hash",
        int? ComparePrefix = null,
        string? ParametersSummary = null);

    private sealed class HuntState
    {
        private readonly ConcurrentDictionary<string, int> _firstSeen = new(StringComparer.Ordinal);
        private readonly ConcurrentBag<CollisionHit> _hits = [];
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private readonly CollisionCampaignKind _kind;
        private readonly object _reportGate = new();
        private long _lastReportTicks;
        private string? _lastDigest;
        private int _completed;

        public HuntState(
            CollisionCampaignKind kind,
            int total,
            ForgeHashParameters parameters,
            int? rngSeed,
            int degreeOfParallelism)
        {
            _kind = kind;
            Total = total;
            Parameters = parameters;
            RngSeed = rngSeed;
            DegreeOfParallelism = degreeOfParallelism;
        }

        public int Total { get; }
        public int Completed => Volatile.Read(ref _completed);
        public int CollisionCount => _hits.Count;
        public ForgeHashParameters Parameters { get; }
        public int? RngSeed { get; }
        public int DegreeOfParallelism { get; }

        public void BumpCompleted() => Interlocked.Increment(ref _completed);

        public void Observe(
            int sampleIndex,
            ReadOnlySpan<byte> digest,
            string channel,
            ReadOnlySpan<byte> password,
            ReadOnlySpan<byte> salt,
            string? parametersSummary,
            int? comparePrefix)
        {
            ReadOnlySpan<byte> keyBytes = comparePrefix is int n
                ? digest[..Math.Min(n, digest.Length)]
                : digest;
            string hex = Convert.ToHexString(keyBytes).ToLowerInvariant();
            string mapKey = channel + ":" + hex;
            Volatile.Write(ref _lastDigest, hex);

            if (!_firstSeen.TryAdd(mapKey, sampleIndex))
            {
                int prior = _firstSeen.TryGetValue(mapKey, out int existing) ? existing : sampleIndex;
                _hits.Add(new CollisionHit(
                    SampleIndex: sampleIndex,
                    PriorSampleIndex: prior,
                    DigestHex: hex,
                    Channel: channel,
                    PasswordHex: Convert.ToHexString(password).ToLowerInvariant(),
                    SaltHex: Convert.ToHexString(salt).ToLowerInvariant(),
                    ParametersSummary: parametersSummary));
            }
        }

        public void ReportThrottled(IProgress<CollisionProgress>? progress, string? message)
        {
            if (progress is null)
            {
                return;
            }

            long now = _watch.ElapsedTicks;
            long last = Interlocked.Read(ref _lastReportTicks);
            // ~10 UI updates/sec
            if (message is null && now - last < Stopwatch.Frequency / 10 && Completed < Total)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _lastReportTicks, now, last) != last && message is null)
            {
                return;
            }

            Report(progress, message);
        }

        public void Report(IProgress<CollisionProgress>? progress, string? message)
        {
            if (progress is null)
            {
                return;
            }

            lock (_reportGate)
            {
                double seconds = Math.Max(_watch.Elapsed.TotalSeconds, 1e-9);
                int done = Completed;
                progress.Report(new CollisionProgress(
                    done,
                    Total,
                    CollisionCount,
                    done / seconds,
                    _watch.Elapsed,
                    _lastDigest,
                    message));
            }
        }

        public CollisionCampaignResult Build(CollisionStopReason reason)
        {
            _watch.Stop();
            double seconds = Math.Max(_watch.Elapsed.TotalSeconds, 1e-9);
            int done = Completed;
            return new CollisionCampaignResult
            {
                Kind = _kind,
                Parameters = Parameters,
                RequestedSamples = Total,
                CompletedSamples = done,
                UniqueDigests = _firstSeen.Count,
                CollisionCount = _hits.Count,
                Collisions = _hits.ToArray(),
                HashesPerSecond = done / seconds,
                Elapsed = _watch.Elapsed,
                StopReason = reason,
                RngSeed = RngSeed,
                DegreeOfParallelism = DegreeOfParallelism,
            };
        }
    }
}
