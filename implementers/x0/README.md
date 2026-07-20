# ForgeHash-X v0 toy vectors

**Experimental. Not for production. Not B3-compatible.**

Frozen digests for the ForgeHash-X sandbox (`forgehx` / `v=0`). See [`docs/forgehx/SPECIFICATION_X.md`](../../docs/forgehx/SPECIFICATION_X.md).

| File | Notes |
|------|--------|
| `manifest.json` | Index + expected digests |
| `vectors/*.json` | Full cases (password, salt, seed, hash, encoded) |
| `kats/forgex_primitive.json` | ForgePerm / ForgeX sponge KATs |
| `CHECKLIST.md` | Porting checklist |
| `OUTLINE.md` | Condensed algorithm outline |
| `verify-vectors.md` | How to claim toy-vector compatibility |

Profile for vectors 1–2: `m=1024`, `t=1`, `p=1`, `out=32`. Vector 3 uses `p=2` at the same memory size.

Regenerate full vectors only via `tools/ForgeHash.X.VectorGen` after an intentional sandbox change. Primitive KATs are frozen independently — bump them only when ForgePerm/ForgeX intentionally changes.
