# ForgeHash

Experimental cryptographic software. Do **not** use it to store production passwords.

Prefer Argon2id, scrypt, bcrypt, or your platform’s password APIs until ForgeHash has had serious independent review.

## What this is

ForgeHash-B3 is a configurable, memory-hard password hashing construction for research, benchmarking, and cross-language ports. The first variant is built on BLAKE3.

| | |
|---|---|
| Algorithm | ForgeHash-B3 |
| Encoded id | `forgeh` |
| Version | `v=1` |
| .NET package | `ForgeHash` `1.0.0-experimental` |

Encoded form:

```text
$forgeh$v=1$m=<memoryKiB>,t=<iterations>,p=<parallelism>$<salt-b64>$<hash-b64>
```

Unpadded RFC 4648 Base64. Parameter order is always `m,t,p`.

## Documentation site

Static docs live in [`website/`](website/). After you push this repo to GitHub, enable Pages (GitHub Actions) — the workflow in [`.github/workflows/pages.yml`](.github/workflows/pages.yml) publishes that folder.

Local preview: open `website/index.html`, or serve the folder:

```bash
npx --yes serve website
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

More detail: [`docs/USAGE.md`](docs/USAGE.md) · sample: `samples/ForgeHash.Sample`

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

Overview: [`langs/README.md`](langs/README.md)  
Porting guide: [`docs/IMPLEMENTING.md`](docs/IMPLEMENTING.md)  
Vector pack: [`implementers/v1/`](implementers/v1/)

Claim **ForgeHash-B3 v1 compatible** only when every official vector matches bit-exactly.

## Build / test (.NET solution)

```bash
dotnet build ForgeHash.sln -c Release
dotnet test ForgeHash.sln -c Release
```

## CLI

```bash
dotnet run --project src/ForgeHash.Cli -- hash --memory 8192 --iterations 1 --parallelism 1
dotnet run --project src/ForgeHash.Cli -- verify "$forgeh$..."
dotnet run --project src/ForgeHash.Cli -- benchmark --memory 8192 --iterations 1 --samples 3
```

## Cost profiles

| Profile | Memory | Iterations | Parallelism |
|---------|--------|------------|-------------|
| Development | 8192 KiB | 1 | 1 |
| Interactive | 65536 KiB | 3 | 1 |
| Sensitive | 262144 KiB | 4 | 2 |

Do not silently pick Development in anything that looks like a production build.

## Spec, vectors, research

- Spec: [`SPECIFICATION.md`](SPECIFICATION.md)
- Frozen vectors: [`implementers/v1/`](implementers/v1/) and [`tests/vectors/`](tests/vectors/)
- Research notes: [`docs/RESEARCH.md`](docs/RESEARCH.md)

```bash
dotnet run --project src/ForgeHash.Visualizer -c Release -- all --out artifacts/analysis --memory 8192 --parallelism 2
```

### Collision Lab (Windows GUI)

Mass uniqueness / collision hunts with live progress (WPF):

```bash
dotnet run --project src/ForgeHash.CollisionLab -c Release
```

Prefer the Development profile (8 MiB) for large `N`. Export JSON/CSV when a run finishes. Shared engine: `ForgeHash.Analysis.CollisionCampaign`.

## Security / license

- [`SECURITY.md`](SECURITY.md)
- [`LICENSE`](LICENSE)
