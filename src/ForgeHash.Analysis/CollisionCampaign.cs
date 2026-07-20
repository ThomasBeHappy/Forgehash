using System.Buffers.Binary;
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
/// </summary>
public static class CollisionCampaign
{
    public static CollisionCampaignResult Run(
        CollisionCampaignKind kind,
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirstCollision = false,
        int? rngSeed = null,
        IProgress<CollisionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleCount, 1);

        try
        {
            return kind switch
            {
                CollisionCampaignKind.DistinctPasswords => RunDistinctPasswords(
                    sampleCount, parameters, stopOnFirstCollision, progress, cancellationToken),
                CollisionCampaignKind.DistinctSalts => RunDistinctSalts(
                    sampleCount, parameters, stopOnFirstCollision, rngSeed, progress, cancellationToken),
                CollisionCampaignKind.RandomPairs => RunRandomPairs(
                    sampleCount, parameters, stopOnFirstCollision, rngSeed, progress, cancellationToken),
                CollisionCampaignKind.NearbyPasswordBitFlips => RunNearbyBitFlips(
                    sampleCount, parameters, stopOnFirstCollision, progress, cancellationToken),
                CollisionCampaignKind.DistinctParameterSets => RunDistinctParameterSets(
                    stopOnFirstCollision, progress, cancellationToken),
                CollisionCampaignKind.TruncatedOutputs => RunTruncatedOutputs(
                    sampleCount, parameters, stopOnFirstCollision, progress, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }
        catch (OperationCanceledException)
        {
            // Caller may still want a partial result; rethrow so UI can mark cancelled.
            throw;
        }
    }

    private static CollisionCampaignResult RunDistinctPasswords(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        int saltLen = Math.Clamp(parameters.SaltLength, 16, 64);
        byte[] salt = new byte[saltLen];
        for (int i = 0; i < saltLen; i++)
        {
            salt[i] = (byte)i;
        }

        var state = new HuntState(CollisionCampaignKind.DistinctPasswords, sampleCount, parameters, null);
        for (int i = 0; i < sampleCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            byte[] password = BitConverter.GetBytes(i);
            if (TrackHashAndSeed(state, i, password, salt, parameters, stopOnFirst, progress))
            {
                return state.Build(CollisionStopReason.StoppedOnFirstCollision);
            }
        }

        return state.Build(CollisionStopReason.Completed);
    }

    private static CollisionCampaignResult RunDistinctSalts(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        int? rngSeed,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        byte[] password = "collision-salt"u8.ToArray();
        int saltLen = Math.Clamp(parameters.SaltLength, 16, 64);
        Random rng = CreateRng(rngSeed);
        var state = new HuntState(CollisionCampaignKind.DistinctSalts, sampleCount, parameters, rngSeed);

        for (int i = 0; i < sampleCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            byte[] salt = new byte[saltLen];
            BinaryPrimitives.WriteInt32LittleEndian(salt, i);
            rng.NextBytes(salt.AsSpan(4));
            if (TrackHashAndSeed(state, i, password, salt, parameters, stopOnFirst, progress))
            {
                return state.Build(CollisionStopReason.StoppedOnFirstCollision);
            }
        }

        return state.Build(CollisionStopReason.Completed);
    }

    private static CollisionCampaignResult RunRandomPairs(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        int? rngSeed,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        int saltLen = Math.Clamp(parameters.SaltLength, 16, 64);
        Random rng = CreateRng(rngSeed);
        var state = new HuntState(CollisionCampaignKind.RandomPairs, sampleCount, parameters, rngSeed);

        for (int i = 0; i < sampleCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            byte[] password = new byte[16];
            byte[] salt = new byte[saltLen];
            BinaryPrimitives.WriteInt32LittleEndian(password, i);
            BinaryPrimitives.WriteInt32LittleEndian(salt, i ^ 0x5a5a5a5a);
            rng.NextBytes(password.AsSpan(4));
            rng.NextBytes(salt.AsSpan(4));
            if (TrackHash(state, i, password, salt, parameters, "hash", null, stopOnFirst, progress))
            {
                return state.Build(CollisionStopReason.StoppedOnFirstCollision);
            }
        }

        return state.Build(CollisionStopReason.Completed);
    }

    private static CollisionCampaignResult RunNearbyBitFlips(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
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

        var state = new HuntState(CollisionCampaignKind.NearbyPasswordBitFlips, bitCount, parameters, null);
        byte[] baseline = ForgeHash.DeriveHash(basePassword, salt, parameters);
        state.Observe(-1, baseline, "hash", basePassword, salt, null, null);
        state.Report(progress, "baseline");

        for (int bitIndex = 0; bitIndex < bitCount; bitIndex++)
        {
            ct.ThrowIfCancellationRequested();
            int byteIndex = bitIndex / 8;
            int bit = bitIndex % 8;
            byte[] flipped = (byte[])basePassword.Clone();
            flipped[byteIndex] ^= (byte)(1 << bit);
            if (TrackHash(state, bitIndex, flipped, salt, parameters, "hash", null, stopOnFirst, progress))
            {
                return state.Build(CollisionStopReason.StoppedOnFirstCollision);
            }
        }

        return state.Build(CollisionStopReason.Completed);
    }

    private static CollisionCampaignResult RunDistinctParameterSets(
        bool stopOnFirst,
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

        var state = new HuntState(CollisionCampaignKind.DistinctParameterSets, sets.Length, sets[0], null);
        for (int i = 0; i < sets.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            ForgeHashParameters p = sets[i];
            string summary = $"m={p.MemoryKiB},t={p.Iterations},p={p.Parallelism},out={p.OutputLength}";
            if (TrackHash(state, i, password, salt, p, "hash-prefix16", 16, stopOnFirst, progress, summary))
            {
                return state.Build(CollisionStopReason.StoppedOnFirstCollision);
            }
        }

        return state.Build(CollisionStopReason.Completed);
    }

    private static CollisionCampaignResult RunTruncatedOutputs(
        int sampleCount,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        IProgress<CollisionProgress>? progress,
        CancellationToken ct)
    {
        var truncated = parameters with { OutputLength = 16 };
        byte[] salt = new byte[Math.Clamp(truncated.SaltLength, 16, 64)];
        var state = new HuntState(CollisionCampaignKind.TruncatedOutputs, sampleCount, truncated, null);

        for (int i = 0; i < sampleCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            byte[] password = [(byte)i, (byte)(i >> 8), 0xAA, 0xBB];
            if (TrackHash(state, i, password, salt, truncated, "hash", null, stopOnFirst, progress))
            {
                return state.Build(CollisionStopReason.StoppedOnFirstCollision);
            }
        }

        return state.Build(CollisionStopReason.Completed);
    }

    private static bool TrackHashAndSeed(
        HuntState state,
        int sampleIndex,
        byte[] password,
        byte[] salt,
        ForgeHashParameters parameters,
        bool stopOnFirst,
        IProgress<CollisionProgress>? progress)
    {
        byte[] hash = ForgeHash.DeriveHash(password, salt, parameters);
        state.Observe(sampleIndex, hash, "hash", password, salt, null, null);
        state.BumpCompleted();
        if (stopOnFirst && state.CollisionCount > 0)
        {
            state.Report(progress, "hash collision");
            return true;
        }

        byte[] seed = ForgeHash.ComputeSeed(password, salt, parameters);
        state.Observe(sampleIndex, seed, "seed", password, salt, null, null);
        state.Report(progress, null);
        return stopOnFirst && state.CollisionCount > 0;
    }

    private static bool TrackHash(
        HuntState state,
        int sampleIndex,
        byte[] password,
        byte[] salt,
        ForgeHashParameters parameters,
        string channel,
        int? comparePrefix,
        bool stopOnFirst,
        IProgress<CollisionProgress>? progress,
        string? parametersSummary = null)
    {
        byte[] hash = ForgeHash.DeriveHash(password, salt, parameters);
        state.Observe(sampleIndex, hash, channel, password, salt, parametersSummary, comparePrefix);
        state.BumpCompleted();
        state.Report(progress, null);
        return stopOnFirst && state.CollisionCount > 0;
    }

    private static Random CreateRng(int? seed)
        => seed is null ? Random.Shared : new Random(seed.Value);

    private sealed class HuntState
    {
        private readonly Dictionary<string, int> _firstSeen = new(StringComparer.Ordinal);
        private readonly List<CollisionHit> _hits = [];
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private readonly CollisionCampaignKind _kind;
        private string? _lastDigest;

        public HuntState(CollisionCampaignKind kind, int total, ForgeHashParameters parameters, int? rngSeed)
        {
            _kind = kind;
            Total = total;
            Parameters = parameters;
            RngSeed = rngSeed;
        }

        public int Total { get; }
        public int Completed { get; private set; }
        public int CollisionCount => _hits.Count;
        public ForgeHashParameters Parameters { get; }
        public int? RngSeed { get; }

        public void BumpCompleted() => Completed++;

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
            _lastDigest = hex;

            if (_firstSeen.TryGetValue(mapKey, out int prior))
            {
                _hits.Add(new CollisionHit(
                    SampleIndex: sampleIndex,
                    PriorSampleIndex: prior,
                    DigestHex: hex,
                    Channel: channel,
                    PasswordHex: Convert.ToHexString(password).ToLowerInvariant(),
                    SaltHex: Convert.ToHexString(salt).ToLowerInvariant(),
                    ParametersSummary: parametersSummary));
            }
            else
            {
                _firstSeen[mapKey] = sampleIndex;
            }
        }

        public void Report(IProgress<CollisionProgress>? progress, string? message)
        {
            if (progress is null)
            {
                return;
            }

            double seconds = Math.Max(_watch.Elapsed.TotalSeconds, 1e-9);
            progress.Report(new CollisionProgress(
                Completed,
                Total,
                CollisionCount,
                Completed / seconds,
                _watch.Elapsed,
                _lastDigest,
                message));
        }

        public CollisionCampaignResult Build(CollisionStopReason reason)
        {
            _watch.Stop();
            double seconds = Math.Max(_watch.Elapsed.TotalSeconds, 1e-9);
            return new CollisionCampaignResult
            {
                Kind = _kind,
                Parameters = Parameters,
                RequestedSamples = Total,
                CompletedSamples = Completed,
                UniqueDigests = _firstSeen.Count,
                CollisionCount = _hits.Count,
                Collisions = _hits.ToList(),
                HashesPerSecond = Completed / seconds,
                Elapsed = _watch.Elapsed,
                StopReason = reason,
                RngSeed = RngSeed,
            };
        }
    }
}
