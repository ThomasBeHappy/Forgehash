# forgeh (Rust)

Experimental **ForgeHash-B3 v1** implementation.

**Not for production password storage.**

## Build / test

```bash
cargo test --manifest-path langs/rust/forgeh/Cargo.toml --release
```

## Library

```rust
use forgeh::{hash_password, verify_password, Params};

let encoded = hash_password(b"secret", &Params::development())?;
assert!(verify_password(b"secret", &encoded));
```

## C ABI

Build a `cdylib` / `staticlib` and link:

- `forgeh_derive_hash(...)`
- `forgeh_verify_password(...)`
- `forgeh_encode(...)`

Used by the C++ and PHP packages under `langs/cpp/forgeh` and `langs/php/forgeh`.
