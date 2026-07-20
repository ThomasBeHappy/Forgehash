# Research index

Experimental cryptography only. Not a security proof.

## Start here

**Full write-up:** [`RESEARCH_REPORT.md`](RESEARCH_REPORT.md)  
**On the docs site:** [research-report.html](../website/research-report.html) (published via GitHub Pages)

The report covers construction summary, conformance, the 100 000-sample uniqueness campaign (0 collisions, 2026-07-20), reference/TMTO snapshots, automated checks, risks, and open questions.

## Tools

| Tool | Command |
|------|---------|
| Collision Lab (Windows GUI) | `dotnet run --project src/ForgeHash.CollisionLab -c Release` |
| Visualizer / artifacts | `dotnet run --project src/ForgeHash.Visualizer -c Release -- all --out artifacts/analysis --memory 8192 --iterations 1 --parallelism 2` |
| Benchmarks | `dotnet run --project src/ForgeHash.Benchmarks -c Release` |
| Automated suites | `dotnet test tests/ForgeHash.Tests -c Release` |

Engine shared by CI and the lab: `ForgeHash.Analysis.CollisionCampaign`. Prefer Development (8192 KiB) for large `N`. Raise **Workers** for multi-core sample hashing; each worker holds a full memory matrix.

## Scope snapshot

| Topic | Status |
|-------|--------|
| Official v1 vectors | Frozen |
| Collision campaigns | CI + GUI (incl. 1e5 random pairs, 0 hits) |
| Avalanche / memory influence / references | Automated |
| TMTO stride heuristic | Diagnostic only |
| GPU / ASIC / side-channel lab | Open |

## Warning

A green suite or a clean mass campaign shows consistency with the specification. It does **not** show that ForgeHash is safe for production passwords.
