# ForgeHash research notes

Experimental cryptography only. Not a security proof.

Drawn from `ForgeHash.Analysis` / `ForgeHash.Visualizer` / `ForgeHash.CollisionLab` and the suites under `tests/ForgeHash.Tests`.

## Collision Lab (GUI)

Windows WPF app for mass collision / uniqueness hunts:

```bash
dotnet run --project src/ForgeHash.CollisionLab -c Release
```

Campaign kinds: distinct passwords, distinct salts, random pairs, nearby bit-flips, distinct parameter sets, truncated 16-byte outputs. Progress shows samples completed, H/s, and hits; results export to JSON or CSV.

Engine: `ForgeHash.Analysis.CollisionCampaign` (same path the xUnit smoke tests use). Stick to Development (8192 KiB) unless you intentionally want a slow run.

## Scope

Investigations covered by the current tooling:

| Topic | Status | Where |
|-------|--------|-------|
| Official v1 vectors | Frozen | `tests/vectors/` |
| Collision campaigns | Automated + GUI | `CollisionCampaign` / `CollisionTests.cs` / `ForgeHash.CollisionLab` |
| Avalanche (~50% bit flips) | Automated | `AvalancheTests.cs` |
| Memory influence / sparse finalization | Automated | `MemoryMutationTests.cs`, `SecurityAnalysisTests.cs` |
| Reference distribution | Automated | `ReferenceDistributionTests.cs` |
| Cross-lane / lane independence | Automated | `SecurityAnalysisTests.cs` |
| Reference predictability smoke check | Automated | `SecurityAnalysisTests.cs` |
| TMTO retention ladder | Heuristic model | `ReferenceAnalysis.EstimateStandardTmtoLadder` |
| Parser abuse / allocation limits | Automated | `ParserAttackTests.cs` |
| Dependency graph / heat map / CSV | Tooling | `ForgeHash.Visualizer` |
| GPU / ASIC evaluation | Open | Not instrumented yet |
| Side-channel lab measurement | Open | Documented risk only |

## How to regenerate artifacts

```bash
dotnet run --project src/ForgeHash.Visualizer -c Release -- all --out artifacts/analysis --memory 8192 --iterations 1 --parallelism 2
```

Outputs:

- `report.json` — combined metrics
- `references.csv` — raw block references
- `heatmap.json` — reference frequencies
- `lanes.json` — cross-lane interactions
- `graph.dot` — Graphviz dependency graph
- `tmto.json` — retention / miss-rate estimates
- `RESEARCH_NOTES.md` — short narrative snapshot

## Collision campaign summary

The shared engine hunts for accidental equality across:

- distinct passwords / fixed salt
- distinct salts / fixed password
- single-bit password neighbors
- distinct parameter sets
- short (16-byte) research outputs
- random password/salt pairs

CI runs small `N` and fails on any collision. The GUI runs the same logic at larger `N`. These are empirical smoke hunts, not birthday-bound proofs.

## TMTO model caveat

The TMTO ladder counts how often a retained-block policy would miss a referenced block, then applies a simple `1 + missRate * (keepEvery/2)` extra ForgeMix factor. This is a **diagnostic heuristic**, not an adversarial lower bound.

## Known intentional risks

- Password-dependent addressing improves some cracking-hardware friction and increases local side-channel exposure.
- Do not deploy ForgeHash where fine-grained memory/timing observation is in scope unless that risk is accepted.

## Open questions

1. Can a smarter TMTO store irregular checkpoints cheaper than the stride model suggests?
2. Does ForgeMix map to GPU shared-memory/register pressure efficiently?
3. Are there short cycles in reference graphs under adversarial salts?
4. How large is timing leakage from password-dependent indices on shared cloud hosts?

## Final warning

A green test suite shows internal consistency with the v1 specification. It does **not** show that ForgeHash is safe for production passwords.
