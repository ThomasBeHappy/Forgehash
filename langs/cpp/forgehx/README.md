# ForgeHash-X (C++)

**Experimental. Not for production. Not B3-compatible.**

Thin C++20 API over the Rust `forgehx` C ABI.

```bash
cargo build --release --manifest-path langs/rust/forgehx/Cargo.toml
cmake -S langs/cpp/forgehx -B langs/cpp/forgehx/build
cmake --build langs/cpp/forgehx/build --config Release
./langs/cpp/forgehx/build/Release/forgehx_vector_test   # Windows path may vary
```
