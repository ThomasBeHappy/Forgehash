# Verifying a ForgeHash-B3 v1 port

## 1. Load the pack

Read `manifest.json`, then each file under `vectors/`.

## 2. Assert core fields

For every vector:

```text
ComputeSeed(password, salt, params)     == seedHex
DeriveHash(password, salt, params)      == hashHex
Encode(params, salt, hash)              == encoded
```

## 3. Assert intermediates (recommended)

If your implementation can expose internals:

```text
initialized block prefixes              == initializedBlocks[].prefixHex
selected references                     == sampleReferences[]
ForgeMix sample prefixes                == forgeMixSamples[].prefixHex
group root                              == groupRootHex
```

## 4. Negative parser tests

Reject at least:

```text
$forgeh$v=1$t=3,m=65536,p=1$...
$forgeh$v=01$m=65536,t=3,p=1$...
$forgeh$v=1$m=065536,t=3,p=1$...
$forgeh$v=1$m=65536, t=3,p=1$...
```

## 5. Compatibility statement

Only after all vectors pass:

```text
ForgeHash-B3 v1 compatible (experimental; not for production)
```
