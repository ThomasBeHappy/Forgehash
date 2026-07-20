# forgeh (PHP)

Experimental ForgeHash-B3 via **PHP FFI** to the Rust core.

**Not for production password storage.**

## Requirements

- PHP 8.1+ with `ext-ffi` enabled
- Built Rust library (`forgeh.dll` / `libforgeh.so` / `libforgeh.dylib`)

```bash
cargo build --release --manifest-path langs/rust/forgeh/Cargo.toml
php langs/php/forgeh/tests/vectors.php
```

Optional: `FORGEH_LIB=/absolute/path/to/forgeh.dll`

## Usage

```php
use ForgeH\ForgeHash;

$encoded = ForgeHash::hashPassword('secret', ForgeHash::development());
$ok = ForgeHash::verifyPassword('secret', $encoded);
```
