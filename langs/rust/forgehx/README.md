# forgehx (Rust)

**Experimental. Not for production. Not compatible with ForgeHash-B3.**

ForgeHash-X v0 (`$forgehx$v=0$…`) with a custom ForgeX sponge — no BLAKE3 dependency.

Vectors: `implementers/x0/vectors/`. Spec: [`docs/forgehx/SPECIFICATION_X.md`](../../../docs/forgehx/SPECIFICATION_X.md).

## Build / test

```bash
cargo test --manifest-path langs/rust/forgehx/Cargo.toml --release
```

## Library

```rust
use forgehx::{hash_password, verify_password, Params};

let encoded = hash_password(b"secret", &Params::toy())?;
assert!(verify_password(b"secret", &encoded));
```

## C ABI

`cdylib` / `staticlib` exports:

- `forgehx_derive_hash`
- `forgehx_derive_seed`
- `forgehx_verify_password`
