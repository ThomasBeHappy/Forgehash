# Verifying a ForgeHash-X v0 port

**Experimental. Not for production. Not B3-compatible.**

## 1. Load the pack

Read `manifest.json`, then each file under `vectors/`. Optionally load `kats/forgex_primitive.json`.

## 2. Assert core fields

For every vector:

```text
ComputeSeed(password, salt, params)     == seedHex
DeriveHash(password, salt, params)      == hashHex
Encode(params, salt, hash)              == encoded
```

## 3. Assert primitives (recommended)

```text
RC(0,0) / RC(7,15)                      == kats.roundConstants
Permute(zeros)                          == kats.zeroStatePermute
ForgeX.Hash / Xof cases                 == kats.hash / kats.xof
```

## 4. Negative parser tests

Reject at least:

```text
$forgehx$v=0$t=1,m=1024,p=1$...
$forgehx$v=00$m=1024,t=1,p=1$...
$forgehx$v=0$m=01024,t=1,p=1$...
$forgeh$v=1$m=1024,t=1,p=1$...          # B3 id must not parse as X
```

## 5. Compatibility statement

Only after all toy vectors (and recommended KATs) pass:

```text
ForgeHash-X v0 toy-vector compatible (experimental; not for production; not B3)
```
