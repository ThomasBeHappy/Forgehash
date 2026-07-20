# ForgeHash-X v0 implementation checklist

Mark each item only after it is covered by tests against the toy vectors / KATs.
**Experimental. Not for production. Not B3-compatible.**

## Primitives

- [ ] LE32 / LE64 helpers
- [ ] ForgePerm round constants `RC(r,i)`
- [ ] ForgePerm quarter-round (wrapping `2^64`, Low32 multiply, rotates 17/11/23/41)
- [ ] ForgePerm 8 rounds: RC → column QRs → diagonal QRs → perm `(7i+3) mod 16`
- [ ] ForgeX sponge: 16×u64 state, rate 8 words (64 bytes)
- [ ] Domain absorb: `LE32(tagLen) || ASCII(tag) || data`
- [ ] Pad + squeeze (32-byte hash / XOF)
- [ ] Primitive KATs in `kats/forgex_primitive.json`

## Memory

- [ ] Contiguous **512-byte** blocks / 64×u64 LE words
- [ ] Init blocks `[lane][0]` and `[lane][1]` via Expand
- [ ] Pass loop + 4 slices + barrier semantics
- [ ] Pass 0 starts at block index 2
- [ ] Cross-lane only when `blockIndex % 16 == 0`
- [ ] Cross-lane allowed region = completed slices only
- [ ] Never read uninitialized blocks

## Finalization

- [ ] Lane accumulator samples (last / ¼ / ½ / ¾)
- [ ] Root with `ForgeX/v0/final`
- [ ] Output XOF with `ForgeX/v0/output`

## Encoding / API

- [ ] Canonical `$forgehx$v=0$m,t,p$salt$hash` (unpadded Base64)
- [ ] Reject reordered params / leading zeros / whitespace
- [ ] Validate params before allocation
- [ ] Constant-time verify
- [ ] No Unicode normalization / no silent truncation
- [ ] Reject `$forgeh$` (B3) strings in the X parser

## Vector gates

- [ ] vector1 hash + seed + encoded
- [ ] vector2 hash + seed + encoded
- [ ] vector3 hash + seed + encoded (+ parallel match if implemented)
