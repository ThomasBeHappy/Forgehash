# ForgeHash-B3 v1 implementer pack

Bit-exact compatibility kit for ports.

ForgeHash is experimental. Passing these vectors does not make an implementation safe for production password storage.

## Contents

| Path | Purpose |
|------|---------|
| `manifest.json` | Index of official vectors |
| `vectors/*.json` | Intermediate snapshots + expected digests |
| `CHECKLIST.md` | Implementation checklist |
| `OUTLINE.md` | Condensed algorithm steps |
| `verify-vectors.md` | How to assert compatibility |

## Regenerate from the .NET reference

```bash
dotnet run --project tools/ForgeHash.VectorGen -c Release -- tests/vectors
# then sync into this pack:
dotnet run --project tools/ForgeHash.VectorGen -c Release -- implementers/v1/vectors
# (or copy tests/vectors/vector*.json here and refresh manifest)
```
