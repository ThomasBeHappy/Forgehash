# ForgeHash-B3 v1 implementation checklist

Mark each item only after it is covered by tests against the official vectors.

## Primitives

- [ ] LE32 / LE64 helpers
- [ ] BLAKE3 derive-key with context `ForgeHash/v1/seed`
- [ ] BLAKE3 XOF expand with prefix `ForgeHash/v1/expand`
- [ ] FastRange = high64(x * n) using 128-bit math
- [ ] ForgeMix quarter-round (wrapping `2^64`, Low32 multiply)
- [ ] ForgeMix 8 rounds: rows → columns → diagonals → perm `(73i+19) mod 128`
- [ ] Feed-forward XOR

## Memory

- [ ] Contiguous 1024-byte blocks / 128×u64 LE words
- [ ] Init blocks `[lane][0]` and `[lane][1]` via Expand
- [ ] Pass loop + 4 slices + barrier semantics
- [ ] Pass 0 starts at block index 2
- [ ] Previous wrap at block 0 for later passes
- [ ] Pass 0 write = ForgeMix(combined)
- [ ] Later passes write = old ⊕ ForgeMix(combined)
- [ ] Address word + FastRange reference index
- [ ] Cross-lane only when `blockIndex % 32 == 0`
- [ ] Cross-lane allowed region = completed slices only
- [ ] Never read uninitialized blocks

## Finalization

- [ ] Lane accumulator samples at last / ¼ / ½ / ¾
- [ ] 64-block group digests with `ForgeHash/v1/group`
- [ ] Incremental group-root with `ForgeHash/v1/group-root`
- [ ] Root with `ForgeHash/v1/final`
- [ ] Output XOF with `ForgeHash/v1/output`

## Encoding / API

- [ ] Canonical `$forgeh$v=1$m,t,p$salt$hash` (unpadded Base64)
- [ ] Reject reordered params / leading zeros / whitespace
- [ ] Validate params before allocation
- [ ] Constant-time verify
- [ ] No Unicode normalization / no silent truncation

## Vector gates

- [ ] vector1 hash + encoded
- [ ] vector2 hash + encoded
- [ ] vector3 hash + encoded (+ parallel match if implemented)
- [ ] vector4 hash + encoded (+ parallel match if implemented)
- [ ] seed / groupRoot / sampled intermediates for each vector
