# ForgeHash-B3 v1 — Empirical research notes

**Status:** living notes for researchers and implementers  
**Algorithm:** ForgeHash-B3, encoded id `forgeh`, version `v=1`  
**Date of this write-up:** 2026-07-20  

> ForgeHash is experimental cryptographic software. Nothing in this document
> is a security proof, certification, or recommendation to store production
> passwords. Prefer Argon2id, scrypt, bcrypt, or platform password APIs until
> the construction has received substantial independent review.
>
> AI tools assisted some drafting; most of this document was written by hand.
> See [`../AI.md`](../AI.md).

ForgeHash-X (`forgehx` / `v=0`) is a separate experimental sandbox. Its campaign
log lives in [`forgehx/RESEARCH_NOTES.md`](forgehx/RESEARCH_NOTES.md) — do not
mix digests or cost profiles with this B3 document.

---

## 1. Purpose

ForgeHash-B3 is a configurable, memory-hard password hashing construction built
on BLAKE3. The project exists to:

1. Publish a complete, versioned specification with frozen test vectors.
2. Ship bit-identical reference implementations in several languages.
3. Provide tooling for empirical study (collision hunts, reference graphs,
   avalanche smoke tests, TMTO heuristics).

This document records what the current tooling has measured, how to reproduce
those measurements, and where the gaps are.

---

## 2. Construction (short)

Full normative text: [`SPECIFICATION.md`](../SPECIFICATION.md). Condensed outline:
[`implementers/v1/OUTLINE.md`](../implementers/v1/OUTLINE.md).

| Piece | Role |
|-------|------|
| Encoded input | Little-endian parameter + password + salt framing |
| Seed | BLAKE3 derive-key, context `ForgeHash/v1/seed` |
| Expand | BLAKE3 XOF with prefix `ForgeHash/v1/expand` |
| Memory | Contiguous 1024-byte blocks (128×u64 LE words) |
| ForgeMix | 8 rounds of row/column/diagonal mixing + perm + feed-forward |
| Addressing | Password-dependent `FastRange` (high 64 of 128-bit product) |
| Lanes / slices | `p` lanes, 4 slices per pass, barrier after each slice |
| Finalization | Lane samples + 64-block group digests + group-root + root + output XOF |
| Encoding | `$forgeh$v=1$m=…,t=…,p=…$salt$hash` (unpadded Base64, fixed field order) |

Recommended profiles (spec §33):

| Profile | Memory | Iterations | Parallelism |
|---------|--------|------------|-------------|
| Development | 8192 KiB | 1 | 1 |
| Interactive | 65536 KiB | 3 | 1 |
| Sensitive | 262144 KiB | 4 | 2 |

Development exists for tests and mass campaigns. Do not silently select it in
anything that looks like a production build.

---

## 3. Conformance baseline

Official vectors are frozen under [`implementers/v1/`](../implementers/v1/) and
[`tests/vectors/`](../tests/vectors/). An implementation may claim
**ForgeHash-B3 v1 compatible** only when all vectors match bit-exactly.

### Final digests (hex)

| Vector | Description | `hashHex` |
|--------|-------------|-----------|
| 1 | empty password, zero salt; 8 MiB / t=1 / p=1 | `50aa2141813479be95d66a46efa0e076191addb64d1cf0a3cc832c6c9b54be4e` |
| 2 | `password` / `00..0f` salt; 8 MiB / t=1 / p=1 | `02acdfa7faa0f149fe700b2f46b792fda8eaecd5f14844142c67709c561a6a98` |
| 3 | UTF-8 password; 16 MiB / t=2 / p=2 | `fc2f2e6bbcda6f7ca4a927d8a827b7224b30fcce829c8418005d7dacf6f061ba` |
| 4 | null bytes; 32 MiB / t=3 / p=4 | `158230bd7d23be110989b6e9c26a408a0109c2f8ab95d5294e7558a7c9b40b3d` |

### Language status (as of this write-up)

| Implementation | Style | Official vectors |
|----------------|-------|------------------|
| .NET (`src/ForgeHash.Core`) | Reference | pass |
| Rust (`langs/rust/forgeh`) | Native + C ABI | pass |
| Node.js (`langs/nodejs/forgeh`) | Native JS | pass |
| Python (`langs/python/forgeh`) | Native Python | pass |
| C++ (`langs/cpp/forgeh`) | Wrapper over Rust C ABI | smoke / CMake |
| PHP (`langs/php/forgeh`) | FFI over Rust | needs `ext-ffi` |

Porting checklist: [`implementers/v1/CHECKLIST.md`](../implementers/v1/CHECKLIST.md).  
Guide: [`IMPLEMENTING.md`](IMPLEMENTING.md).

---

## 4. Tooling map

| Tool | Path | Role |
|------|------|------|
| Spec | `SPECIFICATION.md` | Normative algorithm |
| .NET core | `src/ForgeHash.Core` | Reference library |
| Analysis | `src/ForgeHash.Analysis` | Traces, TMTO heuristic, collision engine |
| Visualizer | `src/ForgeHash.Visualizer` | Export report / CSV / DOT / TMTO JSON |
| Collision Lab | `src/ForgeHash.CollisionLab` | Windows GUI for mass uniqueness hunts |
| CLI | `src/ForgeHash.Cli` | hash / verify / benchmark / vector |
| Benchmarks | `src/ForgeHash.Benchmarks` | BenchmarkDotNet |
| Vector gen | `tools/ForgeHash.VectorGen` | Regenerate frozen vector JSON |
| Tests | `tests/ForgeHash.Tests` | Determinism, parser, avalanche, collisions, … |

---

## 5. Collision campaigns

### 5.1 What is measured

The shared engine `ForgeHash.Analysis.CollisionCampaign` (used by both xUnit and
the WPF lab) looks for accidental equality of digests across:

- distinct passwords, fixed salt (also tracks seeds)
- distinct salts, fixed password (also tracks seeds)
- random password + salt pairs
- nearby password bit-flips
- a small matrix of distinct parameter sets (16-byte hash prefix compare)
- truncated 16-byte research outputs

CI runs small `N` (48–64). The lab runs large `N` with configurable worker
threads (each in-flight hash holds a full memory matrix).

These are empirical uniqueness smoke hunts. They are **not** birthday-bound
collision searches and do **not** bound adversarial collision probability.

### 5.2 Campaign log — 100 000 random pairs (2026-07-20)

| Field | Value |
|-------|-------|
| Kind | `RandomPairs` |
| Samples | 100 000 |
| Collisions | **0** |
| Parameters | Development (8192 KiB, t=1, p=1, output 32) |
| Engine | `CollisionCampaign` via Collision Lab |
| Export | `forgeh-collision-RandomPairs-20260720-103850.csv` (header only; empty hit list) |

Interpretation: under Development cost, no accidental final-hash collision was
observed among 10⁵ independently generated random password/salt pairs. That is
encouraging consistency data. It does not imply collisions are impossible, and
it says nothing about second-preimage or structured adversarial inputs.

Reproduce (GUI):

```bash
dotnet run --project src/ForgeHash.CollisionLab -c Release
```

Select **Random password + salt pairs**, set `N=100000`, Development profile,
raise **Workers** to match CPU count, Start, export CSV/JSON when finished.

Reproduce (library sketch):

```csharp
var result = CollisionCampaign.Run(
    CollisionCampaignKind.RandomPairs,
    sampleCount: 100_000,
    ForgeHashParameters.Development,
    maxDegreeOfParallelism: Environment.ProcessorCount);
// expect result.CollisionCount == 0
```

---

## 6. Reference-graph / TMTO snapshot

Generated with:

```bash
dotnet run --project src/ForgeHash.Visualizer -c Release -- all --out artifacts/analysis --memory 8192 --iterations 1 --parallelism 2
```

Example snapshot (`artifacts/analysis/report.json`, Development-scale, `p=2`):

| Metric | Value |
|--------|-------|
| Total references | 8188 |
| Cross-lane references | 97 (~1.18%) |
| Pass-0 early-region share (heuristic) | ~0.60 |

Stride retention TMTO heuristic (same run):

| keepEvery | Retention | Miss rate | Est. extra ForgeMix factor |
|----------:|----------:|----------:|---------------------------:|
| 1 | 100% | 0% | 1.00× |
| 2 | 50% | ~50.3% | ~1.50× |
| 4 | 25% | ~74.6% | ~2.49× |
| 8 | 12.5% | ~87.5% | ~4.50× |

**Caveat:** the ladder counts how often a “keep every *k*-th block” policy would
miss a referenced block, then applies a simple
`1 + missRate × (keepEvery/2)` extra-mix factor. That is a diagnostic model for
comparing retention policies. It is **not** an adversarial TMTO lower bound and
does not model irregular checkpoints, compression, or GPU batching.

---

## 7. Other automated checks

| Check | What it asserts | Location |
|-------|-----------------|----------|
| Avalanche smoke | Single-bit password flips change ~35–65% of output bits | `AvalancheTests.cs` |
| Memory influence | Mutating distant blocks changes finalization | `MemoryMutationTests.cs` |
| Reference distribution | Coverage / non-degeneracy of addressing | `ReferenceDistributionTests.cs` |
| Parallel ≡ sequential | Lane-parallel fill matches sequential digests | `ParallelEquivalenceTests.cs` |
| Parser hardness | Rejects reordered fields, leading zeros, whitespace, padded Base64 | `ParserTests.cs`, `ParserAttackTests.cs` |
| Constant-time verify path | Uses fixed-time compare for digests | `ConstantTimeTests.cs` |

Passing these shows internal consistency with the specification. It does not
show resistance to cryptanalysis.

---

## 8. Intentional design risks

Password-dependent addressing is intentional:

- It can raise the cost of some cracking-hardware schedules that assume
  data-independent access.
- It also increases exposure to fine-grained timing / cache / memory side
  channels on the verifying host.

Do not deploy ForgeHash where such observation is in scope unless that trade-off
is explicitly accepted. The project does not currently ship side-channel lab
measurements.

---

## 9. Open questions

1. Can an adversary store irregular checkpoints cheaper than the stride TMTO
   model suggests?
2. How well does ForgeMix map onto GPU shared memory and register pressure?
3. Are there short cycles or pathological salts in the reference graph?
4. What is the practical timing leakage of password-dependent indices on shared
   cloud CPUs?
5. How do Interactive / Sensitive profiles behave under the same 10⁵-scale
   uniqueness campaigns (cost-limited today)?
6. Independent re-implementation review outside this repository.

---

## 10. Reproduction checklist

```bash
# Vectors (.NET)
dotnet test tests/ForgeHash.Tests -c Release --filter Official

# Vectors (Rust / Node / Python)
cargo test --manifest-path langs/rust/forgeh/Cargo.toml --release
cd langs/nodejs/forgeh && npm test
cd langs/python/forgeh && pytest -q

# Analysis bundle
dotnet run --project src/ForgeHash.Visualizer -c Release -- all --out artifacts/analysis --memory 8192 --iterations 1 --parallelism 2

# Mass collision lab (Windows)
dotnet run --project src/ForgeHash.CollisionLab -c Release
```

Docs site (GitHub Pages source must be **GitHub Actions**, publishing `website/`):

- https://thomasbehappy.github.io/Forgehash/

---

## 11. Related documents

| Document | Contents |
|----------|----------|
| [`SPECIFICATION.md`](../SPECIFICATION.md) | Full algorithm |
| [`RESEARCH.md`](RESEARCH.md) | Short tooling index |
| [`IMPLEMENTING.md`](IMPLEMENTING.md) | Porting guide |
| [`USAGE.md`](USAGE.md) | .NET API |
| [`SECURITY.md`](../SECURITY.md) | Disclosure policy |
| [`implementers/v1/`](../implementers/v1/) | Vector pack + checklist |

---

## 12. Closing

ForgeHash-B3 v1 is ready for **experimentation, porting, and measurement**.
It is not ready for protecting production credentials. Treat every green
campaign — including the 100 000-sample uniqueness run — as a consistency
signal, not a security claim.
