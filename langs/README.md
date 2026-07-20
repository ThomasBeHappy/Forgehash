# Multi-language packages

Experimental cryptography. Not for production password storage.

| Language | Path | Style | Vectors |
|----------|------|-------|---------|
| Rust | [rust/forgeh](rust/forgeh) | Full native + C ABI | all 4 pass |
| Node.js | [nodejs/forgeh](nodejs/forgeh) | Full native JS | all 4 pass |
| Python | [python/forgeh](python/forgeh) | Full native Python | all 4 pass |
| C++ | [cpp/forgeh](cpp/forgeh) | C++20 API over Rust C ABI | smoke / CMake |
| PHP | [php/forgeh](php/forgeh) | PHP 8.1 FFI over Rust | needs `ext-ffi` + built lib |

Official vectors: [implementers/v1](../implementers/v1).

## Commands

```bash
# Rust
cargo test --manifest-path langs/rust/forgeh/Cargo.toml --release

# Node.js
cd langs/nodejs/forgeh && npm install && npm test

# Python
cd langs/python/forgeh && python -m pip install -e ".[dev]" && pytest -q

# C++ (build Rust core first)
cargo build --release --manifest-path langs/rust/forgeh/Cargo.toml
cmake -S langs/cpp/forgeh -B langs/cpp/forgeh/build
cmake --build langs/cpp/forgeh/build --config Release

# PHP
php langs/php/forgeh/tests/vectors.php
```

## Compatibility

An implementation may claim ForgeHash-B3 v1 compatible only when all official vectors match bit-exactly. See [docs/IMPLEMENTING.md](../docs/IMPLEMENTING.md).
