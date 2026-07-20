# ForgeHash-X (experimental sandbox)

**Not for production password storage. Not reviewed. Not compatible with ForgeHash-B3.**

ForgeHash-X is a clean-sheet research track: custom **ForgeX** sponge + memory-hard construction, encoded as `$forgehx$v=0$…`. ForgeHash-B3 (`$forgeh$v=1$…`) is unchanged.

| | |
|---|---|
| Spec | [`SPECIFICATION_X.md`](SPECIFICATION_X.md) |
| Research notes | [`RESEARCH_NOTES.md`](RESEARCH_NOTES.md) |
| **Research paper (PDF)** | [`paper/ForgeHash_X_Research_Paper.pdf`](paper/ForgeHash_X_Research_Paper.pdf) |
| .NET core | `src/ForgeHash.X.Core/` |
| Toy vectors | [`implementers/x0/`](../../implementers/x0/) |
| Primitive KATs | [`implementers/x0/kats/`](../../implementers/x0/kats/) |
| Tests | `tests/ForgeHash.X.Tests/` |
| Cross-impl checks | `tests/ForgeHash.CrossImplementation.Tests/` + `tools/crosscheck/` |
| Sample | `samples/ForgeHash.X.Sample/` |
| Language ports | `langs/{python,nodejs,rust,cpp,php}/forgehx/` |
| CI | `.github/workflows/forgehx.yml` |

## Language ports

```bash
# Python / Node / Rust (native ForgeX — no BLAKE3)
cd langs/python/forgehx && python -m pip install -e ".[dev]" && python -m pytest -q
cd langs/nodejs/forgehx && npm install && npm test
cargo test --manifest-path langs/rust/forgehx/Cargo.toml --release

# C++ / PHP wrap the Rust C ABI
cargo build --release --manifest-path langs/rust/forgehx/Cargo.toml
```

## CLI

```bash
dotnet run --project src/ForgeHash.Cli -- hash --algo x --memory 1024 --iterations 1 --password-stdin
dotnet run --project src/ForgeHash.Cli -- verify "$forgehx$v=0$..." --password-stdin
dotnet run --project src/ForgeHash.Cli -- vector --algo x --password-hex "" --salt-hex 00000000000000000000000000000000 --memory 1024
```

## Build / test

```bash
dotnet build src/ForgeHash.X.Core/ForgeHash.X.Core.csproj -c Release
dotnet test tests/ForgeHash.X.Tests -c Release
```

Regenerate toy vectors (only when intentionally bumping the sandbox):

```bash
dotnet run --project tools/ForgeHash.X.VectorGen -c Release
```

## API sketch

```csharp
using ForgeHashX;
using ForgeHashXApi = ForgeHashX.ForgeHashX;

byte[] hash = ForgeHashXApi.DeriveHash(password, salt, ForgeHashXParameters.Toy);
string encoded = ForgeHashXApi.HashPassword(password, ForgeHashXParameters.Toy);
bool ok = ForgeHashXApi.VerifyPassword(password, encoded);
```

Namespace is `ForgeHashX` (class `ForgeHashX`) so it does not collide with the B3 `ForgeHash` package.

## Research throughput

```bash
# B3 vs X at the same (m,t,p)
dotnet run --project tools/ForgeHash.ResearchBench -c Release -- --suite matched --markdown

# vs bcrypt / Argon2id / scrypt / PBKDF2 (OWASP-adjacent presets — not equal work)
dotnet run --project tools/ForgeHash.ResearchBench -c Release -- --suite peers --markdown --out artifacts/research/peer_kdf_bench.md
```

Results: [`RESEARCH_NOTES.md`](RESEARCH_NOTES.md) §3 (matched) and §3b (peers).

Optional BenchmarkDotNet short-run: `dotnet run --project src/ForgeHash.Benchmarks -c Release -- --filter *MatchedCost*`

## Collision Lab

`src/ForgeHash.CollisionLab` can hunt B3 or X. Choose **ForgeHash-X** in the Algorithm combo and the **Toy** preset for sandbox mass runs. Campaigns use the shared `CollisionCampaign` engine with `XCollisionHasher`.


