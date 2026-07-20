# forgehx (Node.js)

**Experimental. Not for production. Not compatible with ForgeHash-B3.**

Pure JavaScript (ESM) reference of ForgeHash-X v0 (`forgehx`) with a custom ForgeX sponge — no BLAKE3 dependency.

```bash
cd langs/nodejs/forgehx
npm install
npm test
```

## Usage

```js
import { hashPassword, verifyPassword, Params } from "forgehx";

const encoded = hashPassword("secret", Params.toy());
const ok = verifyPassword("secret", encoded);
```

Low-level:

```js
import { deriveHash, deriveSeed, encode, Params } from "forgehx";

const params = Params.toy();
const salt = Buffer.from("000102030405060708090a0b0c0d0e0f", "hex");
const hash = deriveHash(Buffer.from("password"), salt, params);
const encoded = encode(params, salt, hash);
```

Vectors: `implementers/x0/vectors/`. Spec: `docs/forgehx/SPECIFICATION_X.md`.
