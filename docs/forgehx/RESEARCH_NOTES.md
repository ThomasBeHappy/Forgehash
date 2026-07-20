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

For comparison: B3’s Development profile uses **8192 KiB** (~8× more memory
than X Toy; ~2× the 4 MiB X smoke). Throughput comparisons across unmatched
presets are not apples-to-apples.

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

### Throughput (informal)

On the same machine/session, Toy mass runs reported on the order of
**~2000 hashes/s**. That is expected to look much faster than B3 Development
(~8 MiB matrices) primarily because Toy uses **1 MiB** matrices. Re-benchmark
at matched memory (e.g. both at 8192 KiB, once X allows that cost) before
drawing performance conclusions about ForgeX vs BLAKE3.

---

## 3. Reproduce

```bash
dotnet run --project src/ForgeHash.CollisionLab -c Release
```

1. Algorithm: **ForgeHash-X (forgehx)**
2. Preset: **Toy**
3. Campaign: one of the kinds above
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

## 4. What is still open for X

- Matched-cost benchmarks vs B3 (same `m,t,p`)
- ForgePerm / sponge known-answer tests beyond construction vectors
- Broader higher-cost uniqueness smoke (e.g. more kinds at 4–8 MiB; matched B3 Dev)
- Language ports against `implementers/x0`
- Independent cryptanalysis (none yet)

---

## 5. Final warning

Passing toy vectors and large-N uniqueness hunts show the .NET reference is
stable and digests separate under these sample sets. They do not show that
ForgeHash-X is secure.
