# forgeh (Node.js)

Experimental ForgeHash-B3 v1 reference in plain JavaScript (ESM).

**Not for production password storage.**

## Install / test

```bash
cd langs/nodejs/forgeh
npm install
npm test
```

Depends on `blake3@^2.1.7` (v3 on npm currently fails to install).

## Usage

```js
import { hashPassword, verifyPassword, Params } from "forgeh";

const encoded = hashPassword("secret", Params.interactive());
const ok = verifyPassword("secret", encoded);
```

Low-level:

```js
import { deriveHash, deriveSeed, encode, Params } from "forgeh";

const params = Params.development(); // 8 MiB — tests only
const salt = Buffer.from("000102030405060708090a0b0c0d0e0f", "hex");
const hash = deriveHash(Buffer.from("password"), salt, params);
const encoded = encode(1, params, salt, hash);
```

| Profile | Memory | Iterations | Lanes |
|---------|--------|------------|-------|
| `development()` | 8 MiB | 1 | 1 |
| `interactive()` | 64 MiB | 3 | 1 |
| `sensitive()` | 256 MiB | 4 | 2 |

Official vectors live in `implementers/v1/vectors/`. The suite is intentionally slow (memory-hard + BigInt hot path).
