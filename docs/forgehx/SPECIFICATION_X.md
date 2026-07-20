# ForgeHash-X Specification (v0 sandbox)

**Experimental research software. Not for production password storage.**  
**Not a security proof. Not compatible with ForgeHash-B3.**

ForgeHash-X is a clean-sheet memory-hard password hashing construction that uses a
custom sponge primitive (**ForgeX**) instead of BLAKE3. Version `v=0` is a
sandbox: algorithms and encodings may change without a migration path.

ForgeHash-B3 (`$forgeh$v=1$…`) remains the frozen BLAKE3-based line. This
document does not alter B3.

---

## 1. Identity

| Item | Value |
|------|-------|
| Algorithm name | ForgeHash-X |
| Encoded id | `forgehx` |
| Version | `v=0` |
| Primitive | ForgeX sponge (custom) |
| Default output | 32 bytes |

Encoded form:

```text
$forgehx$v=0$m=<memoryKiB>,t=<iterations>,p=<parallelism>$<salt-b64>$<hash-b64>
```

Base64 is RFC 4648 without padding. Parameter order is always `m,t,p`.

---

## 2. Goals / non-goals

**Goals**

- Custom sponge + custom memory-hard fill with no external hash dependency
- Bit-stable toy vectors for research ports
- Clear separation from ForgeHash-B3

**Non-goals (v0)**

- Production readiness or cryptographic certification
- Compatibility with `$forgeh$` digests
- GPU/ASIC evaluation, side-channel hardening claims

---

## 3. Parameters (sandbox)

| Parameter | Symbol | Sandbox bounds |
|-----------|--------|----------------|
| Memory | `memoryKiB` | 256 … 65536 (KiB of RAM) |
| Iterations | `iterations` | 1 … 8 |
| Parallelism | `parallelism` | 1 … 16 |
| Output length | `outputLength` | 16 … 64 bytes |
| Salt length | — | 16 … 64 bytes |

Constraints:

- `memoryKiB * 1024` must be divisible by `BlockSize` (512)
- `blockCount = memoryKiB * 1024 / 512` must be divisible by `parallelism`
- `blocksPerLane = blockCount / parallelism` must be divisible by 4
- `blocksPerLane >= 8`

Toy-vector profile:

```text
memoryKiB = 1024
iterations = 1
parallelism = 1
outputLength = 32
saltLength = 16
```

---

## 4. ForgeX sponge

### 4.1 State

- 16 little-endian `u64` words → 1024-bit state `S[0..15]`
- Rate `R = 8` words (512 bits / 64 bytes)
- Capacity `C = 8` words (512 bits)
- Rate bytes occupy the first 64 bytes of the state (`S[0..7]`)

Initial state: all zeros.

### 4.2 Domain tags

ASCII strings, absorbed as:

```text
LE32(tagByteLength) || tagBytes
```

| Tag string | Role |
|------------|------|
| `ForgeX/v0/seed` | Initial seed |
| `ForgeX/v0/expand` | Memory init XOF |
| `ForgeX/v0/final` | Final root |
| `ForgeX/v0/output` | Output XOF |

### 4.3 ForgePerm (permutation)

ForgePerm transforms the 16-word state in place. It runs **8 rounds**.

#### Round constants

For round `r` in `0..7` and word `i` in `0..15`:

```text
RC[r][i] = rotl64(0x9E3779B97F4A7C15 ^ (u64(r) * 0xD1B54A32D192ED03) ^ (u64(i) * 0xA24BAED4963EE407), (r + i*3) mod 64)
```

where `rotl64` is 64-bit rotate-left and `^` is XOR. All arithmetic for the
multiplies is wrapping `u64`.

At the start of round `r`, for each `i`: `S[i] ^= RC[r][i]`.

#### Quarter-round

On words `(a,b,c,d)` (indices into `S`), wrapping `u64` arithmetic:

```text
Low32(x) = x & 0xFFFFFFFF

a = a + b + 2 * Low32(a) * Low32(b)
d = rotr64(d XOR a, 17)
c = c + d + 2 * Low32(c) * Low32(d)
b = rotr64(b XOR c, 11)
a = a + b + 2 * Low32(a) * Low32(b)
d = rotr64(d XOR a, 23)
c = c + d + 2 * Low32(c) * Low32(d)
b = rotr64(b XOR c, 41)
```

#### Round body

After injecting `RC[r]`:

1. Column quarter-rounds on `(0,4,8,12)`, `(1,5,9,13)`, `(2,6,10,14)`, `(3,7,11,15)`
2. Diagonal quarter-rounds on `(0,5,10,15)`, `(1,6,11,12)`, `(2,7,8,13)`, `(3,4,9,14)`
3. Word permutation into a temporary `T`: `T[(i * 7 + 3) mod 16] = S[i]` for `i=0..15`, then `S ← T`

### 4.4 Absorb

Maintain a byte buffer into the rate (64 bytes).

`Absorb(bytes)`:

1. XOR successive input bytes into the current rate offset
2. When the rate is full, run `ForgePerm(S)` and reset the rate offset to 0

`AbsorbDomain(tag, payload)`:

```text
Absorb(LE32(len(tag)) || tag)
Absorb(payload)
```

### 4.5 Pad and switch

Before the first squeeze after absorbs:

```text
rate[offset] ^= 0x01
rate[R_BYTES - 1] ^= 0x80
ForgePerm(S)
offset = 0
```

(`R_BYTES = 64`.)

### 4.6 Squeeze

`Squeeze(n)` returns `n` bytes:

1. Copy bytes from the rate at `offset`
2. When the rate is exhausted, `ForgePerm(S)` and set `offset = 0`

### 4.7 Convenience

```text
ForgeX-Hash(tag, data)     = AbsorbDomain(tag, data); Pad; Squeeze(32)
ForgeX-XOF(tag, data, n)   = AbsorbDomain(tag, data); Pad; Squeeze(n)
```

Each call starts from a fresh zero state.

---

## 5. Little-endian helpers

```text
LE32(x) — 4 bytes
LE64(x) — 8 bytes
```

---

## 6. Seed

```text
material =
  LE32(0)                      // algorithm family id for X sandbox
  || LE32(memoryKiB)
  || LE32(iterations)
  || LE32(parallelism)
  || LE32(outputLength)
  || LE64(passwordLength)
  || password
  || LE32(saltLength)
  || salt

seed = ForgeX-Hash("ForgeX/v0/seed", material)   // 32 bytes
```

---

## 7. Memory layout

- `BlockSize = 512` bytes = 64 × `u64` LE words  
- `blockCount = memoryKiB * 1024 / 512`  
- `blocksPerLane = blockCount / parallelism`  
- `sliceLength = blocksPerLane / 4`  

Blocks are stored contiguously in lane-major order:
`flatIndex = lane * blocksPerLane + blockIndex`.

---

## 8. Initialization

For each lane `L` in `0 .. parallelism-1` and `i` in `{0,1}`:

```text
block[L][i] = ForgeX-XOF(
  "ForgeX/v0/expand",
  seed || LE32(L) || LE32(i),
  512)
```

---

## 9. Reference selection

Let `prev` be the previous block in the current lane (64 words).

```text
addressWord =
  prev[0]
  XOR rotl64(prev[9], 19)
  XOR prev[31]
  XOR u64(pass)
  XOR rotl64(u64(blockIndex), 11)

if parallelism == 1:
  referenceLane = 0
else if blockIndex % 16 == 0:
  referenceLane = FastRange(prev[1], parallelism)
else:
  referenceLane = currentLane

FastRange(x, n) = high64( (u128)x * (u128)n )
```

Allowed region:

```text
if referenceLane == currentLane:
  allowed = (pass == 0) ? blockIndex : blocksPerLane
else:
  allowed = slice * sliceLength   // completed slices only
```

If `allowed == 0`, force `referenceLane = currentLane` and recompute `allowed`.

```text
referenceIndex = FastRange(addressWord, allowed)
```

---

## 10. Block mix

Input: 64-word block `B`, plus `pass`, `lane`, `blockIndex` as `u64`.

Split `B` into four 16-word chunks `C0..C3`.

For chunk index `k` in `0..3`:

```text
state = Ck
state[0] ^= pass
state[1] ^= lane
state[2] ^= blockIndex
state[3] ^= rotl64(pass + blockIndex + u64(k), 13)
ForgePerm(state)
Ck_out = state XOR Ck   // feed-forward
```

Output block is `C0_out || C1_out || C2_out || C3_out`.

---

## 11. Memory filling

For `pass` in `0 .. iterations-1`:  
  for `slice` in `0 .. 3`:  
    for each lane (sequential or parallel; barrier after each slice):  
      process blocks in that slice:

```text
start = slice * sliceLength
end = start + sliceLength
if pass == 0 and slice == 0: start = 2

for blockIndex in start .. end-1:
  previousIndex = (blockIndex > 0) ? blockIndex - 1 : blocksPerLane - 1
  prev = block[lane][previousIndex]
  (refLane, refIndex) = SelectReference(...)
  ref = block[refLane][refIndex]
  combined = prev XOR ref   // word-wise
  mixed = BlockMix(combined, pass, lane, blockIndex)
  if pass == 0:
    block[lane][blockIndex] = mixed
  else:
    block[lane][blockIndex] = block[lane][blockIndex] XOR mixed
```

---

## 12. Finalization

```text
last = blocksPerLane - 1
q1 = blocksPerLane / 4
q2 = blocksPerLane / 2
q3 = (blocksPerLane * 3) / 4

fold = 64 zero words
for each lane:
  for index in {last, q1, q2, q3}:
    fold ^= block[lane][index]

foldBytes = LE serialization of fold (512 bytes)

root = ForgeX-Hash(
  "ForgeX/v0/final",
  seed || foldBytes || LE32(memoryKiB) || LE32(iterations) || LE32(parallelism) || LE32(outputLength))

output = ForgeX-XOF("ForgeX/v0/output", root || seed, outputLength)
```

---

## 13. Encoding / verification

- Same structural rules as B3 regarding canonical Base64 and strict `m,t,p` order, but algorithm id is `forgehx` and version is `0`
- Reject `forgeh` / `v=1` strings in the X parser
- Verification: recompute digest, constant-time compare

---

## 14. Public API (informative)

```text
DeriveHash(password, salt, params) -> bytes
ComputeSeed(password, salt, params) -> 32 bytes
HashPassword(password, params) -> encoded string   // random salt
VerifyPassword(password, encoded) -> bool
```

---

## 15. Toy vectors

Frozen under `implementers/x0/vectors/`. Regenerated only by intentional sandbox
bumps. Summary (full JSON in-tree):

| Id | Password | Salt | Params | `hashHex` (prefix) |
|----|----------|------|--------|--------------------|
| `vector1_empty_password_zero_salt` | empty | 16×`00` | m=1024,t=1,p=1 | `63def360…` |
| `vector2_short_password_incrementing_salt` | `password` UTF-8 | `00..0f` | m=1024,t=1,p=1 | `7e1916d2…` |
| `vector3_two_lanes_toy` | `x` UTF-8 | 16×`42` | m=1024,t=1,p=2 | `3b050e34…` |

Canonical encoded forms and full digests live in `implementers/x0/manifest.json`.

---

## 16. Final warning

A passing toy-vector suite shows the .NET reference matches this document. It
does not show that ForgeHash-X is secure. Do not store production passwords with
ForgeHash-X or ForgeHash-B3 without extensive independent review.
