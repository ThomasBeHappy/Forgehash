# ForgeHash

Experimental cryptographic software. Do **not** use it to store production passwords.

Prefer Argon2id, scrypt, bcrypt, or your platform’s password APIs until ForgeHash has had serious independent review.

## What this is

**ForgeHash-B3** is a configurable, memory-hard password hashing construction for research, benchmarking, and cross-language ports. The first variant uses BLAKE3.

| | |
|---|---|
| Algorithm | ForgeHash-B3 |
| Encoded id | `forgeh` |
| Version | `v=1` |
| .NET | [`ForgeHash`](https://www.nuget.org/packages/ForgeHash/) · [`ForgeHashX`](https://www.nuget.org/packages/ForgeHashX/) · tool [`ForgeHash.Cli`](https://www.nuget.org/packages/ForgeHash.Cli/) |
| PyPI / npm / crates | [`forgeh`](https://pypi.org/project/forgeh/) · [`forgehx`](https://pypi.org/project/forgehx/) (same names on [npm](https://www.npmjs.com/package/forgeh) / [crates.io](https://crates.io/crates/forgeh)) |
| Spec | [`SPECIFICATION.md`](SPECIFICATION.md) |
| Docs site | https://thomasbehappy.github.io/Forgehash/ |

Encoded form (unpadded RFC 4648 Base64; parameter order always `m,t,p`):

```text
$forgeh$v=1$m=<memoryKiB>,t=<iterations>,p=<parallelism>$<salt-b64>$<hash-b64>
```

## ForgeHash-X (experimental)

Separate research sandbox with a custom **ForgeX** sponge (no BLAKE3). Encoded as `$forgehx$v=0$…`. **Not production-ready, not reviewed, not compatible with B3.** Spec and .NET reference: [`docs/forgehx/`](docs/forgehx/). Toy vectors + KATs: [`implementers/x0/`](implementers/x0/). Sample: `samples/ForgeHash.X.Sample/`. CI: `.github/workflows/forgehx.yml`. Empirical notes: [`docs/forgehx/RESEARCH_NOTES.md`](docs/forgehx/RESEARCH_NOTES.md). Full research paper (PDF): [`docs/forgehx/paper/ForgeHash_X_Research_Paper.pdf`](docs/forgehx/paper/ForgeHash_X_Research_Paper.pdf). Site: [X Vectors](website/vectors-x.html).

## Who should read what

| Audience | Start here |
|----------|------------|
| App developers (.NET) | [`docs/USAGE.md`](docs/USAGE.md) |
| Language porters | [`docs/IMPLEMENTING.md`](docs/IMPLEMENTING.md) + [`implementers/v1/`](implementers/v1/) |
| Researchers | [`docs/RESEARCH_REPORT.md`](docs/RESEARCH_REPORT.md) |
| Cryptographers reviewing the design | [`SPECIFICATION.md`](SPECIFICATION.md) |
| Security reports | [`SECURITY.md`](SECURITY.md) |

## Documentation site

Static HTML under [`website/`](website/), published by GitHub Actions (Pages **Source** must be **GitHub Actions**, not the `/docs` folder).

```bash
npx --yes serve website
```

## Repository layout

```text
SPECIFICATION.md          Normative B3 algorithm
docs/                     Usage, porting, research
docs/forgehx/             ForgeHash-X sandbox spec + README
implementers/v1/          Official B3 vectors + checklist
implementers/x0/          ForgeHash-X toy vectors + ForgeX KATs
samples/ForgeHash.X.Sample/  ForgeHash-X usage demo
src/ForgeHash.Core        .NET B3 reference library
src/ForgeHash.X.Core      .NET X sandbox (ForgeX sponge)
src/ForgeHash.Analysis    Traces, TMTO heuristic, collision engine
src/ForgeHash.CollisionLab  Windows GUI for mass uniqueness hunts
src/ForgeHash.Visualizer  Export analysis artifacts
src/ForgeHash.Cli         hash / verify / benchmark / vector
langs/                    B3 (`forgeh`) + X (`forgehx`) ports
tests/                    xUnit suites + frozen vectors
website/                  GitHub Pages site
```

## Install (experimental prereleases)

```bash
# .NET — https://www.nuget.org/packages/ForgeHash/
dotnet add package ForgeHash --prerelease
dotnet add package ForgeHashX --prerelease
dotnet tool install -g ForgeHash.Cli --prerelease

# Python / Node / Rust
pip install forgeh --pre          # https://pypi.org/project/forgeh/
pip install forgehx --pre         # https://pypi.org/project/forgehx/
npm install forgeh@experimental   # https://www.npmjs.com/package/forgeh
npm install forgehx@experimental  # https://www.npmjs.com/package/forgehx
cargo add forgeh --precise 1.0.0-experimental   # https://crates.io/crates/forgeh
cargo add forgehx --precise 0.1.0-experimental  # https://crates.io/crates/forgehx
```

```csharp
using ForgeHash;
using ForgeHashApi = ForgeHash.ForgeHash; // class name matches the namespace

string encoded = ForgeHashApi.HashPassword(password, ForgeHashParameters.Interactive);
bool ok = ForgeHashApi.VerifyPassword(password, encoded);
```

```bash
forgeh hash --algo b3 --password-stdin
dotnet run --project samples/ForgeHash.Sample -- "demo-password"
```

Publisher ops: [`docs/PUBLISHING.md`](docs/PUBLISHING.md).

## Other languages

| Language | Path | Registry | Notes |
|----------|------|----------|-------|
| Rust | [`langs/rust/forgeh`](langs/rust/forgeh) | [crates.io/forgeh](https://crates.io/crates/forgeh) | Native + C ABI |
| Node.js | [`langs/nodejs/forgeh`](langs/nodejs/forgeh) | [npm/forgeh](https://www.npmjs.com/package/forgeh) | Native JS |
| Python | [`langs/python/forgeh`](langs/python/forgeh) | [PyPI/forgeh](https://pypi.org/project/forgeh/) | Native Python |
| C++ | [`langs/cpp/forgeh`](langs/cpp/forgeh) | — | C++20 over Rust C ABI |
| PHP | [`langs/php/forgeh`](langs/php/forgeh) | — | FFI over Rust (`ext-ffi`) |

X ports use the same registries under `forgehx` / `ForgeHashX`. See [`langs/README.md`](langs/README.md).

Claim **ForgeHash-B3 v1 compatible** only when every official vector matches bit-exactly.

## Build / test

```bash
dotnet build ForgeHash.sln -c Release
dotnet test ForgeHash.sln -c Release
```

## Cost profiles

| Profile | Memory | Iterations | Parallelism |
|---------|--------|------------|-------------|
| Development | 8192 KiB | 1 | 1 |
| Interactive | 65536 KiB | 3 | 1 |
| Sensitive | 262144 KiB | 4 | 2 |

Development is for tests and mass campaigns only.

## Research tooling

**Report:** [`docs/RESEARCH_REPORT.md`](docs/RESEARCH_REPORT.md) (includes a logged run of **100 000** random pairs with **0** collisions at Development cost).

```bash
# Mass uniqueness / collision lab (Windows) — B3 or X via Algorithm combo
dotnet run --project src/ForgeHash.CollisionLab -c Release

# Reference graphs / TMTO heuristic export
dotnet run --project src/ForgeHash.Visualizer -c Release -- all --out artifacts/analysis --memory 8192 --parallelism 2

# BenchmarkDotNet
dotnet run --project src/ForgeHash.Benchmarks -c Release
```

```bash
# CLI
dotnet run --project src/ForgeHash.Cli -- hash --memory 8192 --iterations 1 --parallelism 1
dotnet run --project src/ForgeHash.Cli -- verify "$forgeh$..."
```

## Security / license

- [`SECURITY.md`](SECURITY.md)
- [`LICENSE`](LICENSE)
