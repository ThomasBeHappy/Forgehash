# forgeh (Rust)

Experimental ForgeHash-B3 v1 implementation (native + C ABI).

Not for production password storage.

Verified against the official vectors in `implementers/v1/vectors/`. Spec:
[`SPECIFICATION.md`](../../../SPECIFICATION.md). Research notes:
[`docs/RESEARCH_REPORT.md`](../../../docs/RESEARCH_REPORT.md).

## Build / test

```bash
cargo test --manifest-path langs/rust/forgeh/Cargo.toml --release
cargo build --release --manifest-path langs/rust/forgeh/Cargo.toml
```

## Library

```rust
use forgeh::{hash_password, verify_password, Params};

let encoded = hash_password(b"secret", &Params::development())?;
assert!(verify_password(b"secret", &encoded));
```

## C ABI

`cdylib` / `staticlib` exports:

- `forgeh_derive_hash`
- `forgeh_derive_seed`
- `forgeh_verify_password`
- `forgeh_encode`

Used by `langs/cpp/forgeh` and `langs/php/forgeh`.
