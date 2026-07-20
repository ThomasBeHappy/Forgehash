# Multi-language packages

Experimental cryptography. Not for production password storage.

## ForgeHash-B3 (`forgeh` / `v=1`)

Bit-exact ports against [implementers/v1](../implementers/v1).

| Language | Path | Style | Vectors |
|----------|------|-------|---------|
| Rust | [rust/forgeh](rust/forgeh) | Full native + C ABI | all 4 pass |
| Node.js | [nodejs/forgeh](nodejs/forgeh) | Full native JS | all 4 pass |
| Python | [python/forgeh](python/forgeh) | Full native Python | all 4 pass |
| C++ | [cpp/forgeh](cpp/forgeh) | C++20 API over Rust C ABI | smoke / CMake |
| PHP | [php/forgeh](php/forgeh) | PHP 8.1 FFI over Rust | needs `ext-ffi` + built lib |

## ForgeHash-X (`forgehx` / `v=0` sandbox)

Custom ForgeX sponge — **no BLAKE3**. Not compatible with B3. Toy vectors: [implementers/x0](../implementers/x0). Spec: [docs/forgehx](../docs/forgehx).

| Language | Path | Style | Vectors |
|----------|------|-------|---------|
| Rust | [rust/forgehx](rust/forgehx) | Full native + C ABI | all 3 pass |
| Node.js | [nodejs/forgehx](nodejs/forgehx) | Full native JS | all 3 pass |
| Python | [python/forgehx](python/forgehx) | Full native Python | all 3 pass |
| C++ | [cpp/forgehx](cpp/forgehx) | C++20 API over Rust C ABI | after Rust release build |
| PHP | [php/forgehx](php/forgehx) | PHP 8.1 FFI over Rust | needs `ext-ffi` + built lib |

## Commands

```bash
# B3
cargo test --manifest-path langs/rust/forgeh/Cargo.toml --release
cd langs/nodejs/forgeh && npm install && npm test
cd langs/python/forgeh && python -m pip install -e ".[dev]" && python -m pytest -q

# X
cargo test --manifest-path langs/rust/forgehx/Cargo.toml --release
cd langs/nodejs/forgehx && npm install && npm test
cd langs/python/forgehx && python -m pip install -e ".[dev]" && python -m pytest -q

# CLI (.NET) — B3 or X
dotnet run --project src/ForgeHash.Cli -- hash --algo x --memory 1024 --iterations 1 --parallelism 1 --password-stdin
dotnet run --project src/ForgeHash.Cli -- verify "$forgehx$..." --password-stdin
```

## Compatibility

- **B3 v1 compatible** only when all official `implementers/v1` vectors match.
- **X v0** toy-vector match shows the port follows the sandbox spec — not production safety.
- Matching vectors does not make either algorithm production-safe.
