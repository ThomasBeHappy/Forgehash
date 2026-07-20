# ForgeHash-X v0 — Empirical research notes

**Status:** living notes for the experimental sandbox  
**Algorithm:** ForgeHash-X, encoded id `forgehx`, version `v=0`  
**Date of this write-up:** 2026-07-20  

> ForgeHash-X is experimental research software. Nothing here is a security
> proof, certification, or recommendation to store production passwords.
> It is **not** compatible with ForgeHash-B3. Prefer Argon2id, scrypt, bcrypt,
> or platform password APIs for real password storage.

Normative text: [`SPECIFICATION_X.md`](SPECIFICATION_X.md).  
Full research paper (PDF): [`paper/ForgeHash_X_Research_Paper.pdf`](paper/ForgeHash_X_Research_Paper.pdf).  
B3 research notes (separate track): [`../RESEARCH_REPORT.md`](../RESEARCH_REPORT.md).

---

## 1. Cost profile used for mass hunts

| Profile | Memory | Iterations | Parallelism | Output |
|---------|--------|------------|-------------|--------|
| **Toy** (sandbox default) | 1024 KiB | 1 | 1 | 32 |
| **Custom (4 MiB smoke)** | 4096 KiB | 1 | 1 | 32 |

Toy exists so vectors and mass campaigns stay regenerable on a laptop. It is
**far below** any interactive/sensitive claim. Do not treat Toy throughput or
uniqueness results as evidence about production-cost security.

B3’s Development profile uses **8192 KiB** (algorithm minimum). Matched-cost
throughput comparisons therefore start at 8 MiB — see §3.

---

## 2. Campaign log (2026-07-20)

All runs via Collision Lab → `CollisionCampaign` + `XCollisionHasher`, algorithm
**ForgeHash-X**. Reported collisions are final-hash (and seed where that campaign
tracks seeds).

### 2.1 Toy cost (1024 KiB, t=1, p=1)

| Kind | Samples | Collisions | Notes |
|------|--------:|----------:|-------|
| `RandomPairs` | 100 000 | **0** | Random 16-byte password + salt pairs |
| `NearbyPasswordBitFlips` | 100 000 | **0** | Single-bit neighbors of a base password |
| `DistinctSalts` | 100 000 | **0** | Fixed password, distinct salts (+ seed channel) |
| `DistinctPasswords` | 100 000 | **0** | Distinct passwords, fixed salt (+ seed channel) |

### 2.2 Higher-memory smoke (4096 KiB, t=1, p=1)

| Kind | Samples | Collisions | Notes |
|------|--------:|----------:|-------|
| `DistinctPasswords` | 100 000 | **0** | Distinct passwords, fixed salt (+ seed channel) |

**Interpretation:** across $5×10^5$ samples (four Toy campaigns + one 4 MiB
DistinctPasswords hunt), no accidental digest (or seed, where tracked) collision
was observed. That is useful consistency data for the sandbox. It does **not**
bound adversarial collision probability, birthday-bound risk at larger N,
second-preimage resistance, or behavior at Interactive/Sensitive-scale costs.

---

## 3. Research throughput bench (matched cost)

**Collected:** 2026-07-20 09:49 UTC  
**Machine:** Windows 10.0.26200 · 28 logical CPUs · .NET 9.0.17  
**Tool:** [`tools/ForgeHash.ResearchBench`](../../tools/ForgeHash.ResearchBench/)  
**Method:** warmup=2, samples=7, fixed password/salt, median wall-clock `DeriveHash`.  

**Caveats**

- Single-process laptop timings — not a security or ASIC claim.
- Equal KiB is **not** equal mix work: B3 uses **1024-byte** blocks; X uses **512-byte** blocks. At the same `m`, X performs roughly **2×** as many block mixes.
- Collision Lab multi-worker “~2000 H/s” at Toy was an informal GUI rate and is **not** comparable to this single-threaded median (Toy ≈ **120 H/s** here).

### 3.1 X-only sandbox costs

| Profile | m (KiB) | t | p | median ms | H/s (median) |
|---------|--------:|--:|--:|----------:|-------------:|
| X_Toy | 1024 | 1 | 1 | 8.36 | 119.64 |
| X_4MiB | 4096 | 1 | 1 | 12.15 | 82.33 |

### 3.2 Matched-cost B3 vs X (m ≥ 8192 KiB)

| Cost | B3 median ms | B3 H/s | X median ms | X H/s | B3/X (median time) |
|------|-------------:|-------:|------------:|------:|-------------------:|
| `m=8192,t=1,p=1` | 54.46 | 18.36 | 23.66 | 42.27 | **2.30×** |
| `m=8192,t=1,p=2` | 54.37 | 18.39 | 23.39 | 42.76 | **2.32×** |
| `m=16384,t=1,p=1` | 198.51 | 5.04 | 50.52 | 19.79 | **3.93×** |
| `m=16384,t=2,p=1` | 249.36 | 4.01 | 98.40 | 10.16 | **2.53×** |

Ratio **B3/X > 1** means X finished faster (lower median latency) at that nominal cost.

**Reading:** on this machine, X is ~2.3–3.9× faster wall-clock than B3 at the same
`(m,t,p)`, even though X does more block fills per KiB. That is a **cost-model**
observation about the current .NET references (ForgeMix+BLAKE3 vs ForgePerm/ForgeX),
not evidence that X is “harder” or “safer.” Tuning passes/`m` for equal defender
latency is a separate exercise.

### 3.3 Reproduce matched suite

```bash
dotnet run --project tools/ForgeHash.ResearchBench -c Release -- --suite matched --warmup 2 --samples 7 --markdown --out artifacts/research/matched_cost_bench.md
```

Optional BenchmarkDotNet short-run (matched job):

```bash
dotnet run --project src/ForgeHash.Benchmarks -c Release -- --filter *MatchedCost*
```

CLI one-shot:

```bash
dotnet run --project src/ForgeHash.Cli -c Release -- benchmark --algo x --memory 8192 --iterations 1 --parallelism 1 --samples 7
dotnet run --project src/ForgeHash.Cli -c Release -- benchmark --algo b3 --memory 8192 --iterations 1 --parallelism 1 --samples 7
```

---

## 3b. Peer KDF throughput (bcrypt / Argon2id / scrypt / PBKDF2)

**Collected:** 2026-07-20 09:55 UTC  
**Machine:** same as §3 (Windows · 28 logical CPUs · .NET 9)  
**Tool:** `tools/ForgeHash.ResearchBench --suite peers`  
**Method:** warmup=2, samples=5  

**Libraries:** BCrypt.Net-Next 4.0.3 · Konscious Argon2 1.3.1 · Scrypt.NET 1.3.0 · .NET `Rfc2898DeriveBytes`  

**Hard caveat:** these are **documented / OWASP-adjacent presets**, not equal CPU work, equal memory-hardness, or equal attacker cost. Faster ≠ weaker. ForgeHash remains experimental and is **not** recommended over Argon2id/bcrypt for production.

| Family | Profile | Parameters | median ms | H/s |
|--------|---------|------------|----------:|----:|
| ForgeHash-B3 | Development | m=8192, t=1, p=1 | 69.25 | 14.44 |
| ForgeHash-B3 | Interactive | m=65536, t=3, p=1 | 1445.98 | 0.69 |
| ForgeHash-X | Toy (sandbox) | m=1024, t=1, p=1 | 9.65 | 103.60 |
| ForgeHash-X | Match-8MiB | m=8192, t=1, p=1 | 27.42 | 36.46 |
| ForgeHash-X | Match-64MiB_t3 | m=65536, t=3, p=1 | 669.18 | 1.49 |
| Argon2id | OWASP-min-ish | m=19456 KiB, t=2, p=1 | 40.27 | 24.83 |
| Argon2id | 64MiB_t3_p1 | m=65536 KiB, t=3, p=1 | 187.10 | 5.34 |
| bcrypt | cost-10 | work factor 10 | 52.64 | 19.00 |
| bcrypt | cost-12 | work factor 12 | 201.24 | 4.97 |
| scrypt | N=2^14 | N=16384, r=8, p=1 | 25.56 | 39.12 |
| scrypt | N=2^17 | N=131072, r=8, p=1 | 191.74 | 5.22 |
| PBKDF2-SHA256 | OWASP-600k | 600 000 iters | 54.30 | 18.42 |
| PBKDF2-SHA256 | iter-310k | 310 000 iters | 28.18 | 35.49 |

**Rough latency clustering on this box**

- ~25–70 ms: X Match-8MiB, scrypt N=2^14, PBKDF2-310k, Argon2id 19 MiB, bcrypt cost-10, B3 Development, PBKDF2-600k  
- ~185–210 ms: Argon2id 64 MiB t=3, scrypt N=2^17, bcrypt cost-12  
- ~0.67–1.45 s: X Match-64MiB_t3, B3 Interactive  

Reproduce:

```bash
dotnet run --project tools/ForgeHash.ResearchBench -c Release -- --suite peers --warmup 2 --samples 5 --markdown --out artifacts/research/peer_kdf_bench.md
```

---

## 4. Reproduce uniqueness campaigns

```bash
dotnet run --project src/ForgeHash.CollisionLab -c Release
```

1. Algorithm: **ForgeHash-X (forgehx)**
2. Preset: **Toy**
3. Campaign: one of the kinds in §2
4. Samples: `100000`
5. Workers: ≈ CPU count
6. Start → export JSON/CSV if desired

Library sketch:

```csharp
using ForgeHash.Analysis;
using ForgeHashX;

var cost = CollisionCostSnapshot.FromX(ForgeHashXParameters.Toy);
var result = CollisionCampaign.Run(
    CollisionCampaignKind.RandomPairs,
    sampleCount: 100_000,
    cost,
    XCollisionHasher.Instance,
    maxDegreeOfParallelism: Environment.ProcessorCount);
// expect result.CollisionCount == 0
```

---

## 5. What is still open for X

- Equal-latency parameter search vs Argon2id 64 MiB / bcrypt cost-12 (defender UX targets)
- Broader higher-cost uniqueness smoke at matched 8 MiB
- Independent cryptanalysis of ForgePerm / ForgeX (none yet)
- Cross-machine replication of §3 / §3b; alternate Argon2/scrypt library backends

---

## 6. Final warning

Passing toy vectors, large-N uniqueness hunts, and throughput tables show the
.NET references behave consistently under these sample sets. They do not show
that ForgeHash-X is secure.
