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
| .NET package | `ForgeHash` `1.0.0-experimental` |
| Spec | [`SPECIFICATION.md`](SPECIFICATION.md) |
| Docs site | https://thomasbehappy.github.io/Forgehash/ |

Encoded form (unpadded RFC 4648 Base64; parameter order always `m,t,p`):

```text
$forgeh$v=1$m=<memoryKiB>,t=<iterations>,p=<parallelism>$<salt-b64>$<hash-b64>
```

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
SPECIFICATION.md          Normative algorithm
docs/                     Usage, porting, research
implementers/v1/          Official vectors + checklist
src/ForgeHash.Core        .NET reference library
src/ForgeHash.Analysis    Traces, TMTO heuristic, collision engine
src/ForgeHash.CollisionLab  Windows GUI for mass uniqueness hunts
src/ForgeHash.Visualizer  Export analysis artifacts
src/ForgeHash.Cli         hash / verify / benchmark / vector
langs/                    Rust, Node, Python, C++, PHP
tests/                    xUnit suites + frozen vectors
website/                  GitHub Pages site
```

## .NET

```bash
dotnet pack src/ForgeHash.Core/ForgeHash.Core.csproj -c Release -o artifacts/nuget
dotnet add package ForgeHash --source ./artifacts/nuget --prerelease
```

```csharp
using ForgeHash;
using ForgeHashApi = ForgeHash.ForgeHash; // class name matches the namespace

string encoded = ForgeHashApi.HashPassword(password, ForgeHashParameters.Interactive);
bool ok = ForgeHashApi.VerifyPassword(password, encoded);
```

Sample:

```bash
dotnet run --project samples/ForgeHash.Sample -- "demo-password"
```

## Other languages

| Language | Path | Notes |
|----------|------|-------|
| Rust | [`langs/rust/forgeh`](langs/rust/forgeh) | Native + C ABI; all 4 vectors pass |
| Node.js | [`langs/nodejs/forgeh`](langs/nodejs/forgeh) | Native JS; all 4 vectors pass |
| Python | [`langs/python/forgeh`](langs/python/forgeh) | Native Python; all 4 vectors pass |
| C++ | [`langs/cpp/forgeh`](langs/cpp/forgeh) | C++20 over Rust C ABI |
| PHP | [`langs/php/forgeh`](langs/php/forgeh) | FFI over Rust (`ext-ffi`) |

```bash
cargo test --manifest-path langs/rust/forgeh/Cargo.toml --release
cd langs/nodejs/forgeh && npm install && npm test
cd langs/python/forgeh && python -m pip install -e ".[dev]" && pytest -q
```

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
# Mass uniqueness / collision lab (Windows)
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
