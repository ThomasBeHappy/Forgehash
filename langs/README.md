# Multi-language packages

Experimental cryptography. Not for production password storage.

These packages exist so researchers and porters can verify ForgeHash-B3 v1
bit-exactly against the official vectors without starting from the .NET
reference alone.

| Language | Path | Style | Vectors |
|----------|------|-------|---------|
| Rust | [rust/forgeh](rust/forgeh) | Full native + C ABI | all 4 pass |
| Node.js | [nodejs/forgeh](nodejs/forgeh) | Full native JS | all 4 pass |
| Python | [python/forgeh](python/forgeh) | Full native Python | all 4 pass |
| C++ | [cpp/forgeh](cpp/forgeh) | C++20 API over Rust C ABI | smoke / CMake |
| PHP | [php/forgeh](php/forgeh) | PHP 8.1 FFI over Rust | needs `ext-ffi` + built lib |

Official vectors: [implementers/v1](../implementers/v1).  
Porting guide: [docs/IMPLEMENTING.md](../docs/IMPLEMENTING.md).  
Research notes: [docs/RESEARCH_REPORT.md](../docs/RESEARCH_REPORT.md).

## Commands

```bash
# Rust (also builds the C ABI used by C++/PHP)
cargo test --manifest-path langs/rust/forgeh/Cargo.toml --release
cargo build --release --manifest-path langs/rust/forgeh/Cargo.toml

# Node.js
cd langs/nodejs/forgeh && npm install && npm test

# Python
cd langs/python/forgeh && python -m pip install -e ".[dev]" && pytest -q

# C++ (after Rust release build)
cmake -S langs/cpp/forgeh -B langs/cpp/forgeh/build
cmake --build langs/cpp/forgeh/build --config Release

# PHP
php langs/php/forgeh/tests/vectors.php
```

## Compatibility

An implementation may claim ForgeHash-B3 v1 compatible only when all official
vectors match bit-exactly. Matching vectors does not make the algorithm
production-safe.

## Choosing a starting point

| Goal | Suggested base |
|------|----------------|
| Fastest native re-read of the algorithm | Rust or .NET core |
| Scripting / notebooks | Python |
| Web / tooling in JS | Node.js |
| Embed in C/C++/PHP without re-porting ForgeMix | Rust `cdylib` / `staticlib` + C ABI |
