# Contributing

ForgeHash is experimental research software. Contributions that improve the
specification clarity, vector coverage, ports, analysis tooling, or
documentation are welcome. Contributions that market the algorithm as
production-ready are not.

## Before you start

1. Read [`SPECIFICATION.md`](SPECIFICATION.md) and [`SECURITY.md`](SECURITY.md).
2. Skim [`docs/RESEARCH_REPORT.md`](docs/RESEARCH_REPORT.md).
3. If porting: follow [`docs/IMPLEMENTING.md`](docs/IMPLEMENTING.md) and the
   pack in [`implementers/v1/`](implementers/v1/).

## Ground rules

- Do not remove or soften the experimental / not-for-production warnings.
- Do not claim cryptographic security or certification.
- Keep v1 digests bit-identical. Breaking changes require a new version (`v=2`)
  and new vectors.
- Prefer small, reviewable PRs.
- Match existing code style in the language you touch.

## Useful commands

```bash
dotnet test ForgeHash.sln -c Release
cargo test --manifest-path langs/rust/forgeh/Cargo.toml --release
dotnet run --project src/ForgeHash.CollisionLab -c Release
dotnet run --project src/ForgeHash.Visualizer -c Release -- all --out artifacts/analysis --memory 8192 --parallelism 2
```

## Docs site

Edit files under `website/`. GitHub Pages deploys that folder via
`.github/workflows/pages.yml`. In repo settings, Pages **Source** must be
**GitHub Actions**.

## Security issues

See [`SECURITY.md`](SECURITY.md). Prefer private disclosure for algorithmic or
implementation vulnerabilities.
