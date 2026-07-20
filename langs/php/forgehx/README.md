# ForgeHash-X (PHP)

**Experimental. Not for production. Not B3-compatible.**

PHP 8.1+ FFI wrapper over the Rust `forgehx` shared library.

```bash
cargo build --release --manifest-path langs/rust/forgehx/Cargo.toml
php langs/php/forgehx/tests/vectors.php
```

Requires `ext-ffi` enabled.
