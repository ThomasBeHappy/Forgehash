# forgeh (C++)

C++20 API over the Rust ForgeHash-B3 core (C ABI).

Experimental. Not for production password storage.

## Build

Requires a recent MSVC toolchain (or Clang 20+ with matching STL). First build the Rust core:

```bash
cargo build --release --manifest-path langs/rust/forgeh/Cargo.toml
```

### Quick C ABI smoke test (MSVC)

```bat
call "C:\Program Files\Microsoft Visual Studio\18\Professional\VC\Auxiliary\Build\vcvars64.bat"
cl /O2 langs\cpp\forgeh\tests\c_abi_smoke.c /Fe:langs\cpp\forgeh\c_abi_smoke.exe /link langs\rust\forgeh\target\release\forgeh.lib bcrypt.lib ws2_32.lib userenv.lib ntdll.lib advapi32.lib
langs\cpp\forgeh\c_abi_smoke.exe
```

### CMake (full C++ API + vector suite)

```bash
cmake -S langs/cpp/forgeh -B langs/cpp/forgeh/build -G "Visual Studio 18 2026" -A x64
cmake --build langs/cpp/forgeh/build --config Release
langs\cpp\forgeh\build\Release\forgeh_vector_test.exe
```

## API

```cpp
#include "forgeh.hpp"

auto encoded = forgeh::hash_password("secret", forgeh::Params::development());
bool ok = forgeh::verify_password("secret", encoded);
```
