
# ForgeHash Specification

## 1. Project Overview

ForgeHash is an experimental, configurable, memory-hard password hashing algorithm.

The primary goal is to create a complete password-hashing construction that is:

* expensive to evaluate on CPUs;
* memory-intensive;
* difficult to parallelize efficiently on GPUs;
* resistant to precomputation;
* configurable over time;
* versioned and self-describing;
* suitable for benchmarking and cryptographic experimentation.

ForgeHash is an educational and research project.

It must not be used to protect production passwords unless it has undergone extensive independent cryptographic review. Production applications should continue using established password-hashing algorithms such as Argon2id, scrypt, bcrypt, or approved platform APIs.

The first implementation will use BLAKE3 as the underlying cryptographic primitive.

The initial algorithm variant is named:

```text
ForgeHash-B3
```

A future fully custom primitive may be developed separately as:

```text
ForgeHash-X
```

ForgeHash-X is outside the scope of the first implementation.

---

# 2. Design Goals

ForgeHash should provide the following properties.

## 2.1 Unique salting

Every password hash must use a unique cryptographically random salt.

The salt prevents:

* rainbow-table attacks;
* reuse of precomputed password hashes;
* direct comparison of identical passwords between accounts.

Recommended salt length:

```text
16 bytes
```

Supported salt length:

```text
16 to 64 bytes
```

## 2.2 Configurable cost

ForgeHash must support independently configurable:

* memory usage;
* iteration count;
* lane count;
* output length.

These parameters must be stored alongside the generated hash.

## 2.3 Memory hardness

The algorithm must allocate and repeatedly process a large memory region.

Each generated block must depend on:

* a previous block;
* a pseudorandom reference block;
* the current pass;
* the current lane;
* the current block index.

An implementation that attempts to avoid storing the complete memory region should incur a substantial recomputation cost.

## 2.4 Salt-dependent execution

Memory initialization and addressing must depend on the password, salt, and parameters.

Two hashes using different salts must generate completely different memory contents and access patterns.

## 2.5 Complete-state finalization

The final output must depend on the entire allocated memory region.

The algorithm must not derive its result only from the final block of each lane.

## 2.6 Versioned encoding

Stored hashes must contain:

* algorithm identifier;
* algorithm version;
* memory cost;
* iteration count;
* lane count;
* salt;
* output hash.

The encoded format must support future versions without breaking existing hashes.

## 2.7 Defensive parsing

Encoded hashes are attacker-controlled input.

The parser must validate all parameters before performing memory allocation or expensive computation.

## 2.8 Constant-time verification

Hash comparison must use a constant-time equality function.

The implementation must not compare hash bytes using ordinary early-exit equality logic.

---

# 3. Non-Goals

The initial implementation does not attempt to:

* replace BLAKE3;
* claim formal cryptographic security;
* provide compatibility with Argon2;
* provide deterministic salts;
* encrypt passwords;
* recover passwords;
* provide network-level login rate limiting;
* manage server secrets;
* automatically normalize Unicode passwords;
* guarantee protection against every hardware architecture.

ForgeHash is a password hashing construction, not a general-purpose encryption or hashing replacement.

---

# 4. Terminology

## Password

The secret byte sequence supplied by the user.

## Salt

A public random byte sequence unique to each stored password hash.

## Pepper

An optional secret held by the application and not stored with the password hash.

## Pass

One full traversal over the allocated memory region.

## Lane

A logical section of memory that can be processed partly in parallel.

## Slice

One quarter of a lane.

Each pass is divided into four synchronization slices.

## Block

A fixed-size 1024-byte memory element.

## Memory cost

The total number of kibibytes allocated by the algorithm.

Because each block is 1 KiB:

```text
blockCount = memoryKiB
```

---

# 5. Algorithm Identifier

The canonical algorithm name is:

```text
ForgeHash-B3
```

The compact encoded identifier is:

```text
forgeh
```

Version 1 is represented as:

```text
v=1
```

---

# 6. Encoded Hash Format

The canonical encoded representation is:

```text
$forgeh$v=1$m=<memoryKiB>,t=<iterations>,p=<parallelism>$<salt-base64>$<hash-base64>
```

Example:

```text
$forgeh$v=1$m=65536,t=3,p=1$R6q8M9gNehVdoNG8lE7xFg$22YZbVdZsTwbty7XjF5Y7eKiG8LHPkgZZR4H0WjT2cw
```

The exact example value above is illustrative and not a test vector.

## 6.1 Field definitions

```text
forgeh
```

Algorithm identifier.

```text
v
```

Algorithm version.

```text
m
```

Memory usage in KiB.

```text
t
```

Number of complete memory passes.

```text
p
```

Number of lanes.

```text
salt-base64
```

Salt encoded using unpadded Base64.

```text
hash-base64
```

Hash output encoded using unpadded Base64.

## 6.2 Base64 format

The implementation should use one canonical Base64 representation.

Recommended encoding:

```text
RFC 4648 Base64 without padding
```

The parser must reject malformed Base64.

The parser should not accept multiple alternate encodings for the same byte sequence unless explicitly required for compatibility.

## 6.3 Canonical formatting

Parameter order must always be:

```text
m,t,p
```

Whitespace is not allowed.

Leading plus signs are not allowed.

Leading zeroes should be rejected except for the value zero, which is itself invalid for all current cost parameters.

Examples of invalid encodings:

```text
$forgeh$v=1$t=3,m=65536,p=1$...
$forgeh$v=01$m=65536,t=3,p=1$...
$forgeh$v=1$m=065536,t=3,p=1$...
$forgeh$v=1$m=65536, t=3,p=1$...
```

---

# 7. Parameter Requirements

## 7.1 Memory cost

Parameter:

```text
memoryKiB
```

Minimum algorithm value:

```text
8192 KiB
```

Recommended default:

```text
65536 KiB
```

Recommended upper application limit:

```text
1048576 KiB
```

The application may configure a lower maximum.

Memory must be large enough to provide at least eight blocks per lane.

Required validation:

```text
memoryKiB >= 8192
memoryKiB >= parallelism * 8
```

The memory region should be evenly divisible across lanes.

Required:

```text
blockCount % parallelism == 0
```

Each lane must also be divisible into four equal slices.

Required:

```text
blocksPerLane % 4 == 0
```

## 7.2 Iteration count

Parameter:

```text
iterations
```

Minimum:

```text
1
```

Recommended default:

```text
3
```

Recommended application maximum:

```text
20
```

## 7.3 Parallelism

Parameter:

```text
parallelism
```

Minimum:

```text
1
```

Recommended default:

```text
1
```

Recommended maximum:

```text
logical processor count
```

Absolute implementation maximum:

```text
255
```

## 7.4 Output length

Version 1 encoded hashes use a fixed output length:

```text
32 bytes
```

Internal APIs may support output lengths from 16 to 64 bytes for research and testing.

If variable output lengths are stored in the future, a new encoding field or algorithm version must be introduced.

## 7.5 Salt length

Minimum:

```text
16 bytes
```

Recommended:

```text
16 bytes
```

Maximum:

```text
64 bytes
```

---

# 8. Input Handling

## 8.1 Password representation

The ForgeHash core operates on raw bytes.

The core must not:

* trim whitespace;
* lowercase input;
* uppercase input;
* normalize Unicode;
* remove null bytes;
* treat the password as null-terminated.

String-based convenience APIs should encode strings using UTF-8.

Applications must document whether they apply Unicode normalization before calling ForgeHash.

The recommended default is:

```text
No automatic Unicode normalization
```

This avoids changing the meaning of existing passwords unexpectedly.

## 8.2 Maximum password length

The core may technically support large inputs, but the implementation should enforce a defensive maximum.

Recommended maximum:

```text
1 MiB
```

Authentication applications may use a stricter maximum such as:

```text
4096 bytes
```

The implementation must not silently truncate passwords.

---

# 9. Binary Input Encoding

All values must be encoded unambiguously.

Multi-byte integers use little-endian encoding.

The initial input buffer is constructed as:

```text
LE32(version)
|| LE32(memoryKiB)
|| LE32(iterations)
|| LE32(parallelism)
|| LE32(outputLength)
|| LE64(passwordLength)
|| password
|| LE32(saltLength)
|| salt
|| LE32(contextLength)
|| context
```

For ForgeHash version 1:

```text
version = 1
outputLength = 32
context = empty
contextLength = 0
```

The context field is reserved for future domain separation and application-specific variants.

Implementations must include the zero-length context field even when no context is supplied.

---

# 10. Initial Seed Generation

The initial seed is generated using BLAKE3 key derivation.

Conceptually:

```text
seed = BLAKE3-DeriveKey(
    context = "ForgeHash/v1/seed",
    material = encodedInput
)
```

Seed length:

```text
32 bytes
```

The exact BLAKE3 derive-key operation must follow the official BLAKE3 specification.

Implementations must not substitute ordinary unkeyed hashing while still claiming compatibility.

---

# 11. Memory Layout

The memory region consists of 1024-byte blocks.

```text
blockSize = 1024 bytes
blockCount = memoryKiB
```

Each block is interpreted as:

```text
128 unsigned 64-bit words
```

All word conversions use little-endian byte order.

Memory is divided into lanes.

```text
blocksPerLane = blockCount / parallelism
sliceLength = blocksPerLane / 4
```

Logical indexing:

```text
memory[lane][blockIndex]
```

A flat implementation may calculate:

```text
flatIndex = lane * blocksPerLane + blockIndex
```

The implementation should use contiguous memory where possible.

---

# 12. Expand Function

ForgeHash requires an expansion function that produces arbitrary-length output from a seed and metadata.

Version 1 uses BLAKE3 XOF.

Conceptually:

```text
Expand(input, outputLength) =
    BLAKE3-XOF(
        "ForgeHash/v1/expand" || input,
        outputLength
    )
```

The implementation must apply domain separation exactly.

The string must be encoded as UTF-8 bytes without a null terminator.

---

# 13. Memory Initialization

Each lane begins with two initialized blocks.

For each lane:

```text
block[lane][0] =
    Expand(
        seed
        || LE32(lane)
        || LE32(0),
        1024
    )
```

```text
block[lane][1] =
    Expand(
        seed
        || LE32(lane)
        || LE32(1),
        1024
    )
```

The remaining blocks are generated by the memory-filling phase.

No uninitialized memory may influence computation.

---

# 14. ForgeMix Compression Function

ForgeMix accepts:

* one 1024-byte input block;
* pass index;
* lane index;
* block index.

It returns one 1024-byte output block.

Signature:

```text
ForgeMix(
    inputBlock,
    pass,
    lane,
    blockIndex
) -> outputBlock
```

## 14.1 State loading

Interpret the input block as:

```text
original[0..127]
state[0..127]
```

Initialize:

```text
state[i] = original[i]
```

## 14.2 Position injection

Inject positional metadata:

```text
state[0] ^= LE64(pass)
state[1] ^= LE64(lane)
state[2] ^= LE64(blockIndex)
state[3] ^= RotateLeft64(
    LE64(pass) + LE64(blockIndex),
    17
)
```

The integer values are used directly as unsigned 64-bit values.

The notation `LE64` here describes their canonical byte interpretation in specifications. In the in-memory word implementation, the numeric values are XORed directly.

## 14.3 Quarter-round function

ForgeMix uses the following four-word quarter round:

```text
QuarterRound(ref a, ref b, ref c, ref d):

    a = a + b + 2 * Low32(a) * Low32(b)
    d = RotateRight64(d XOR a, 32)

    c = c + d + 2 * Low32(c) * Low32(d)
    b = RotateRight64(b XOR c, 24)

    a = a + b + 2 * Low32(a) * Low32(b)
    d = RotateRight64(d XOR a, 16)

    c = c + d + 2 * Low32(c) * Low32(d)
    b = RotateRight64(b XOR c, 63)
```

All arithmetic wraps modulo:

```text
2^64
```

`Low32(x)` returns the lower 32 bits of `x` as an unsigned 64-bit value.

The multiplication must also use wrapping unsigned arithmetic.

## 14.4 Round structure

ForgeMix performs eight full rounds.

Each full round contains:

1. row mixing;
2. column mixing;
3. diagonal mixing;
4. word permutation.

The exact word schedule must be fixed and identical across all implementations.

### Row mixing

Treat the state as an 8 × 16 matrix.

For each row:

```text
QuarterRound(row[0], row[4], row[8],  row[12])
QuarterRound(row[1], row[5], row[9],  row[13])
QuarterRound(row[2], row[6], row[10], row[14])
QuarterRound(row[3], row[7], row[11], row[15])
```

Then:

```text
QuarterRound(row[0], row[5], row[10], row[15])
QuarterRound(row[1], row[6], row[11], row[12])
QuarterRound(row[2], row[7], row[8],  row[13])
QuarterRound(row[3], row[4], row[9],  row[14])
```

### Column mixing

Treat each column pair as a 16-word virtual row formed from two adjacent columns.

For each column pair:

```text
virtualRow =
[
    state[row0,colA], state[row0,colB],
    state[row1,colA], state[row1,colB],
    ...
    state[row7,colA], state[row7,colB]
]
```

Apply the same eight quarter-round schedule used for row mixing.

Column pairs:

```text
(0,1)
(2,3)
(4,5)
(6,7)
(8,9)
(10,11)
(12,13)
(14,15)
```

### Diagonal mixing

For each of eight diagonal groups, construct a 16-word group:

```text
group[k * 2] =
    state[k][(base + k) mod 16]

group[k * 2 + 1] =
    state[k][(base + k + 8) mod 16]
```

Where:

```text
base = diagonalIndex * 2
```

Apply the standard row quarter-round schedule to each group.

### Word permutation

After row, column, and diagonal mixing, permute the words:

```text
destinationIndex =
    (sourceIndex * 73 + 19) mod 128
```

Because 73 is coprime with 128, this forms a complete permutation.

Create a temporary array:

```text
permuted[destinationIndex] = state[sourceIndex]
```

Then replace:

```text
state = permuted
```

## 14.5 Feed-forward

After eight full rounds:

```text
output[i] = state[i] XOR original[i]
```

Serialize output words using little-endian byte order.

---

# 15. Reference Block Selection

Each generated block references:

* the previous block in the current lane;
* one pseudorandom reference block.

## 15.1 Address word

The address source is computed from the previous block:

```text
addressWord =
    previous[0]
    XOR RotateLeft64(previous[17], 13)
    XOR previous[73]
    XOR UInt64(pass)
    XOR RotateLeft64(UInt64(blockIndex), 29)
```

## 15.2 Reference lane

For normal blocks:

```text
referenceLane = currentLane
```

For periodic cross-lane blocks:

```text
if blockIndex mod 32 == 0:
    referenceLane =
        FastRange(previous[1], parallelism)
else:
    referenceLane = currentLane
```

When:

```text
parallelism = 1
```

The reference lane is always zero.

## 15.3 FastRange

Map a 64-bit value into a range without ordinary modulo bias:

```text
FastRange(x, n) =
    High64(x * n)
```

Where:

```text
x * n
```

is evaluated as a 128-bit unsigned multiplication and `High64` returns the upper 64 bits.

For languages without native 128-bit integers, implement an equivalent multiply-high operation.

## 15.4 Allowed reference region

During pass zero, a block may reference only memory that has already been initialized within the relevant synchronization rules.

During later passes, the complete reference lane may be used, except where doing so would create a race with blocks currently being overwritten in another worker.

The implementation must define allowed references by slice.

### Pass zero

For the current lane:

```text
allowedBlocks = all blocks before current block
```

For another lane:

```text
allowedBlocks = all blocks in completed slices
```

### Pass one and later

For the current lane:

```text
allowedBlocks = entire lane
```

For another lane:

```text
allowedBlocks = all blocks in completed slices
```

An implementation may include the current slice of another lane only if deterministic synchronization guarantees that the referenced block has already been written.

The reference implementation should use only completed slices for cross-lane access.

## 15.5 Reference index

```text
referenceIndex =
    FastRange(addressWord, allowedBlockCount)
```

Then map the selected local index into the allowed region.

The algorithm must never reference an uninitialized block.

---

# 16. Memory Filling

Each pass is split into four slices.

```text
for pass in 0 .. iterations - 1:
    for slice in 0 .. 3:
        process slice for every lane
        synchronize all lanes
```

## 16.1 Starting position

During pass zero, blocks zero and one are already initialized.

Therefore, the first slice begins processing at block index two.

During later passes, processing starts from the first block of the slice.

## 16.2 Previous block selection

For block index greater than zero:

```text
previousIndex = blockIndex - 1
```

For block zero during later passes:

```text
previousIndex = blocksPerLane - 1
```

This creates a circular dependency between the end and beginning of each lane across passes.

## 16.3 Input block

Compute:

```text
combined =
    previousBlock XOR referenceBlock
```

For pass zero:

```text
newBlock =
    ForgeMix(
        combined,
        pass,
        lane,
        blockIndex
    )
```

For pass one and later:

```text
mixed =
    ForgeMix(
        combined,
        pass,
        lane,
        blockIndex
    )
```

```text
newBlock =
    oldCurrentBlock XOR mixed
```

The current memory block is overwritten with `newBlock`.

All XOR operations are byte-wise or equivalently word-wise.

## 16.4 Lane synchronization

All lanes must complete the current slice before any lane begins the next slice.

A single-threaded implementation naturally satisfies this requirement.

A multithreaded implementation must use a barrier after each slice.

---

# 17. Finalization

Finalization must incorporate the complete memory state.

It consists of:

1. lane sampling;
2. group hashing;
3. root hashing;
4. output expansion.

## 17.1 Lane accumulator

Create a zero-filled 1024-byte accumulator.

For each lane, XOR the following blocks into it:

```text
lane last block
lane block at 1/4 position
lane block at 1/2 position
lane block at 3/4 position
```

Using integer indexes:

```text
last = blocksPerLane - 1
quarter = blocksPerLane / 4
half = blocksPerLane / 2
threeQuarter = (blocksPerLane * 3) / 4
```

Then:

```text
accumulator ^=
    memory[lane][last]
    XOR memory[lane][quarter]
    XOR memory[lane][half]
    XOR memory[lane][threeQuarter]
```

## 17.2 Group digests

Process memory in groups of 64 blocks.

Each group contains:

```text
64 KiB
```

For each group:

```text
groupDigest =
    BLAKE3(
        UTF8("ForgeHash/v1/group")
        || LE64(groupIndex)
        || LE64(groupBlockCount)
        || concatenatedGroupBlocks
    )
```

Digest length:

```text
32 bytes
```

The final group may contain fewer than 64 blocks.

`groupBlockCount` must contain the exact number of blocks in the group.

## 17.3 Group digest reduction

Do not concatenate an unbounded number of group digests into a single allocation.

Feed the group digests into an incremental BLAKE3 hasher:

```text
groupRootHasher.Update(
    UTF8("ForgeHash/v1/group-root")
)
```

For each group:

```text
groupRootHasher.Update(LE64(groupIndex))
groupRootHasher.Update(groupDigest)
```

Finalize:

```text
groupRoot = groupRootHasher.Finalize(32)
```

## 17.4 Root hash

Construct:

```text
root =
    BLAKE3(
        UTF8("ForgeHash/v1/final")
        || seed
        || accumulator
        || groupRoot
        || LE32(memoryKiB)
        || LE32(iterations)
        || LE32(parallelism)
        || LE32(outputLength)
    )
```

Root length:

```text
32 bytes
```

## 17.5 Final output

Generate the final password hash:

```text
hash =
    BLAKE3-XOF(
        UTF8("ForgeHash/v1/output")
        || root
        || seed,
        outputLength
    )
```

For encoded ForgeHash version 1:

```text
outputLength = 32
```

---

# 18. Optional Pepper Support

Pepper handling is outside the core ForgeHash algorithm.

A pepper is a secret application key stored separately from the password database.

Recommended construction:

```text
effectivePassword =
    BLAKE3-KeyedHash(
        key = pepper,
        input =
            UTF8("ForgeHash/v1/pepper")
            || LE64(passwordLength)
            || password
    )
```

Output length:

```text
32 bytes
```

ForgeHash then receives `effectivePassword` as its password input.

The original password is not directly passed to ForgeHash when peppering is enabled.

## 18.1 Pepper requirements

The pepper should:

* contain 32 random bytes;
* come from a cryptographically secure random generator;
* be stored outside the password database;
* be loaded from a secrets manager, hardware security module, or protected application configuration;
* never appear in the encoded hash;
* never be logged.

## 18.2 Pepper identifiers

Applications that rotate peppers may store a separate pepper identifier in the user record.

The pepper identifier is not secret.

Example:

```text
pepper_id = 3
```

The actual pepper value must remain secret.

Pepper identifiers should not be embedded into ForgeHash version 1 unless an application-specific wrapper format is introduced.

---

# 19. Public API

A reference C# API should expose the following structures.

```csharp
public sealed record ForgeHashParameters
{
    public int MemoryKiB { get; init; } = 65_536;
    public int Iterations { get; init; } = 3;
    public int Parallelism { get; init; } = 1;
    public int OutputLength { get; init; } = 32;
    public int SaltLength { get; init; } = 16;
}
```

Main API:

```csharp
public static class ForgeHash
{
    public static string HashPassword(
        ReadOnlySpan<byte> password,
        ForgeHashParameters? parameters = null);

    public static string HashPassword(
        string password,
        ForgeHashParameters? parameters = null);

    public static bool VerifyPassword(
        ReadOnlySpan<byte> password,
        string encodedHash);

    public static bool VerifyPassword(
        string password,
        string encodedHash);

    public static bool NeedsRehash(
        string encodedHash,
        ForgeHashParameters desiredParameters);
}
```

Pepper-aware API:

```csharp
public static string HashPassword(
    ReadOnlySpan<byte> password,
    ReadOnlySpan<byte> pepper,
    ForgeHashParameters? parameters = null);

public static bool VerifyPassword(
    ReadOnlySpan<byte> password,
    ReadOnlySpan<byte> pepper,
    string encodedHash);
```

Low-level research API:

```csharp
public static byte[] DeriveHash(
    ReadOnlySpan<byte> password,
    ReadOnlySpan<byte> salt,
    ForgeHashParameters parameters);
```

The low-level API must not generate a salt automatically.

---

# 20. Verification Procedure

Password verification must perform the following steps.

```text
1. Parse the encoded hash.
2. Confirm the algorithm identifier is supported.
3. Confirm the algorithm version is supported.
4. Validate all parameters.
5. Decode the salt.
6. Decode the expected hash.
7. Recompute ForgeHash using the stored parameters.
8. Compare the expected and actual hashes in constant time.
9. Clear sensitive temporary buffers where practical.
10. Return true or false.
```

Verification should not reveal which parsing or verification stage failed to remote clients.

The public authentication response should generally remain:

```text
Invalid username or password
```

Internal logs may distinguish malformed stored data from invalid passwords, but must not log password material.

---

# 21. NeedsRehash Behavior

`NeedsRehash` returns true when:

* the algorithm identifier is outdated;
* the algorithm version is outdated;
* memory cost is lower than desired;
* iteration count is lower than desired;
* parallelism differs from the current policy;
* output length differs from the current policy;
* salt length is lower than the current policy;
* the encoded representation is valid but non-canonical.

It should not return true merely because stored parameters are stronger than the current minimum.

Example:

```text
stored memory = 262144 KiB
desired memory = 65536 KiB
```

This alone does not require rehashing.

Applications may separately decide to reduce excessive costs, but doing so should be an explicit policy.

---

# 22. Parser Safety

The encoded hash parser must operate defensively.

It must reject:

* unknown algorithms;
* unsupported versions;
* missing fields;
* duplicate fields;
* reordered fields;
* negative values;
* zero values;
* integer overflow;
* values above configured limits;
* malformed Base64;
* salts outside the allowed length;
* output hashes with invalid length;
* trailing data;
* embedded null characters;
* whitespace;
* unexpected delimiters.

## 22.1 Allocation limits

Parameter validation must happen before memory allocation.

Example application policy:

```text
Minimum memory:       8192 KiB
Maximum memory:    1048576 KiB
Minimum iterations:      1
Maximum iterations:     20
Minimum parallelism:     1
Maximum parallelism:    64
Salt length:         16–64 bytes
Output length:          32 bytes
```

A malicious encoded hash must not be able to request arbitrary amounts of memory or CPU time.

---

# 23. Memory Handling

## 23.1 Allocation

The memory region should preferably use one contiguous allocation.

Possible C# options:

* one large `byte[]`;
* one large `ulong[]`;
* unmanaged memory;
* native aligned allocation.

The reference implementation should prioritize correctness over manual memory optimization.

Recommended initial representation:

```text
ulong[]
```

Required word count:

```text
memoryKiB * 128
```

Overflow must be checked before allocation.

## 23.2 Clearing sensitive memory

After hashing, the implementation should clear:

* password byte buffers created by the implementation;
* pepper-derived password buffers;
* initial seed;
* temporary blocks;
* memory matrix;
* root and intermediate digests.

For managed memory, use:

```csharp
CryptographicOperations.ZeroMemory(...)
```

Where possible.

The API cannot guarantee clearing immutable strings supplied by the caller.

Document this limitation.

## 23.3 Pooling

Do not use shared array pools in the first implementation.

Sensitive data may remain in pooled buffers and later become visible to unrelated consumers.

A future optimized version may use secure pooling only if buffers are always cleared before returning them.

---

# 24. Threading Model

## 24.1 Reference implementation

The first implementation must be single-threaded.

This simplifies:

* deterministic behavior;
* debugging;
* test-vector generation;
* memory dependency analysis.

## 24.2 Parallel implementation

A later implementation may process lanes concurrently.

Rules:

* one worker may own one or more lanes;
* lane memory must not be concurrently written by multiple workers;
* all workers synchronize after each slice;
* reference selection must obey the same completed-slice rules;
* single-threaded and multithreaded implementations must produce identical outputs.

The parallelism parameter describes algorithm lanes, not necessarily the exact number of operating-system threads.

---

# 25. Error Handling

Recommended exception types:

```text
ArgumentNullException
ArgumentOutOfRangeException
FormatException
NotSupportedException
CryptographicException
OutOfMemoryException
```

Examples:

* invalid caller-supplied parameters: `ArgumentOutOfRangeException`;
* malformed encoded hash: `FormatException`;
* unsupported algorithm version: `NotSupportedException`;
* cryptographic provider failure: `CryptographicException`.

The verification convenience API may return false for malformed hashes rather than throwing, but a strict parsing API should also be available.

Suggested parser API:

```csharp
public static bool TryParse(
    string encodedHash,
    out ParsedForgeHash parsedHash);
```

---

# 26. Project Structure

Recommended repository layout:

```text
ForgeHash/
├── README.md
├── LICENSE
├── SECURITY.md
├── SPECIFICATION.md
├── ForgeHash.sln
│
├── src/
│   ├── ForgeHash.Core/
│   │   ├── ForgeHash.Core.csproj
│   │   ├── ForgeHash.cs
│   │   ├── ForgeHashEngine.cs
│   │   ├── ForgeHashParameters.cs
│   │   ├── ForgeHashEncoding.cs
│   │   ├── ForgeHashParser.cs
│   │   ├── ForgeMix.cs
│   │   ├── Blake3Adapter.cs
│   │   ├── FastRange.cs
│   │   ├── MemoryMatrix.cs
│   │   └── Internal/
│   │
│   ├── ForgeHash.Cli/
│   │   ├── ForgeHash.Cli.csproj
│   │   └── Program.cs
│   │
│   ├── ForgeHash.Benchmarks/
│   │   ├── ForgeHash.Benchmarks.csproj
│   │   └── Program.cs
│   │
│   └── ForgeHash.Visualizer/
│       ├── ForgeHash.Visualizer.csproj
│       └── Program.cs
│
└── tests/
    ├── ForgeHash.Tests/
    │   ├── ForgeHash.Tests.csproj
    │   ├── DeterminismTests.cs
    │   ├── ParameterTests.cs
    │   ├── EncodingTests.cs
    │   ├── ParserTests.cs
    │   ├── ForgeMixTests.cs
    │   ├── VerificationTests.cs
    │   ├── AvalancheTests.cs
    │   └── TestVectorTests.cs
    │
    └── ForgeHash.CrossImplementation.Tests/
```

Target framework:

```text
.NET 9
```

Language:

```text
C# 13
```

---

# 27. CLI Application

The CLI should support the following commands.

## 27.1 Hash a password

```text
forgeh hash
```

Prompt for a password without echoing it.

Optional parameters:

```text
--memory 65536
--iterations 3
--parallelism 1
--salt-length 16
```

Example:

```text
forgeh hash --memory 65536 --iterations 3 --parallelism 1
```

The CLI must not accept passwords directly as command-line arguments by default because command-line arguments may appear in shell history or process listings.

An explicit testing-only option may be provided:

```text
--password-stdin
```

## 27.2 Verify a password

```text
forgeh verify "<encoded-hash>"
```

Prompt for the password securely.

Exit codes:

```text
0 = password valid
1 = password invalid
2 = malformed hash
3 = internal error
```

## 27.3 Benchmark

```text
forgeh benchmark
```

Options:

```text
--memory
--iterations
--parallelism
--samples
--warmup
```

Output:

```text
average runtime
minimum runtime
maximum runtime
median runtime
allocated memory
effective memory bandwidth
operations per second
```

## 27.4 Generate test vector

```text
forgeh vector
```

The command should accept explicit hexadecimal password and salt input for reproducibility.

Example:

```text
forgeh vector \
    --password-hex 70617373776f7264 \
    --salt-hex 000102030405060708090a0b0c0d0e0f \
    --memory 8192 \
    --iterations 1 \
    --parallelism 1
```

---

# 28. Test Vectors

Official test vectors must include:

* password bytes;
* salt bytes;
* parameters;
* initial seed;
* selected initialized blocks;
* selected reference indexes;
* selected ForgeMix outputs;
* group root;
* final output;
* encoded representation.

At minimum, create vectors for:

```text
Vector 1:
Empty password
16-byte zero salt
8192 KiB
1 iteration
1 lane
```

```text
Vector 2:
ASCII password "password"
Incrementing 16-byte salt
8192 KiB
1 iteration
1 lane
```

```text
Vector 3:
UTF-8 password containing non-ASCII characters
Random fixed salt
16384 KiB
2 iterations
2 lanes
```

```text
Vector 4:
Password containing null bytes
Fixed salt
32768 KiB
3 iterations
4 lanes
```

Do not publish final test-vector values until the implementation and specification are stable.

Any algorithm change before version 1 release must regenerate all vectors.

After version 1 is published, incompatible changes require version 2.

---

# 29. Required Tests

## 29.1 Determinism

The same:

* password;
* salt;
* version;
* parameters;

must always produce the same output.

## 29.2 Salt separation

The same password with different salts must produce different hashes.

## 29.3 Password separation

Different passwords with the same salt must produce different hashes.

## 29.4 Parameter separation

Changing any parameter must alter the output.

Test independently:

* memory;
* iterations;
* parallelism;
* output length in the low-level API;
* version metadata.

## 29.5 Encoding round trip

```text
hash -> encode -> parse -> recompute
```

must succeed.

## 29.6 Canonical encoding

The encoder must always generate one canonical string.

Equivalent non-canonical input forms must be rejected.

## 29.7 Malformed input

Test:

* truncated hashes;
* extra delimiters;
* missing salt;
* missing output;
* invalid Base64;
* duplicate parameters;
* oversized integers;
* negative values;
* excessive memory requests;
* excessive iteration requests;
* unsupported versions.

## 29.8 Constant-time comparison

Use:

```csharp
CryptographicOperations.FixedTimeEquals
```

Do not write a custom comparison unless required for a non-.NET implementation.

## 29.9 Memory influence

Modify individual memory blocks immediately before finalization and confirm that the final hash changes.

Test blocks from:

* beginning;
* first quarter;
* middle;
* third quarter;
* final block;
* multiple lanes.

## 29.10 Avalanche behavior

For fixed salt and parameters:

* flip one bit in the password;
* compare final output;
* measure changed output bits.

Across many samples, the average should approach 50 percent.

This is an analysis metric, not proof of security.

## 29.11 Reference distribution

Collect reference indexes across large test runs.

Check for:

* heavily biased regions;
* unreachable blocks;
* isolated lanes;
* repeated short cycles;
* disproportionate references to early memory.

## 29.12 Parallel equivalence

The sequential and parallel implementations must produce identical outputs for all official test vectors.

---

# 30. Benchmarking

Benchmarking must measure more than total runtime.

Required metrics:

```text
wall-clock time
CPU time
peak working set
allocated managed memory
garbage collections
effective memory bandwidth
lane scaling
iteration scaling
memory scaling
```

Optional hardware counters:

```text
L1 cache misses
L2 cache misses
last-level cache misses
branch mispredictions
memory reads
memory writes
```

Benchmark parameter sets:

```text
8 MiB, 1 pass, 1 lane
16 MiB, 2 passes, 1 lane
64 MiB, 3 passes, 1 lane
64 MiB, 3 passes, 2 lanes
256 MiB, 4 passes, 2 lanes
1 GiB, 4 passes, 4 lanes
```

Do not treat benchmark results as security proof.

---

# 31. Visualizer

Create a tool that records and visualizes block references.

Each processed block should emit:

```text
pass
slice
lane
block index
previous block
reference lane
reference block
cross-lane flag
```

Visualization modes:

## Dependency graph

Show blocks as nodes and references as edges.

## Heat map

Show how often each memory block is referenced.

## Lane interaction view

Show cross-lane references over time.

## Pass comparison

Compare reference patterns between passes.

## Recalculation simulation

Estimate the cost of computing the result while retaining only a fraction of memory.

The visualizer is a research tool and must not be enabled in production hashing paths.

---

# 32. Security Analysis Plan

The project must actively attempt to break ForgeHash.

Questions to investigate:

## Memory reduction

Can an attacker store only every second, fourth, or eighth block and cheaply recompute the rest?

## Reference predictability

Can future block references be predicted without computing previous blocks?

## Lane independence

Can lanes be processed mostly independently?

## Sparse finalization

Can most memory blocks be skipped without changing the final result?

## Time-memory trade-offs

How much additional computation is required when using:

```text
75 percent memory
50 percent memory
25 percent memory
12.5 percent memory
```

## GPU suitability

Does ForgeMix map efficiently to GPU arithmetic?

Does the access pattern produce enough irregular memory traffic?

## ASIC suitability

Can the memory arrangement be significantly simplified in custom hardware?

## Cache behavior

Does most work occur in cache despite requesting large memory values?

## Collision behavior

Can distinct passwords, salts, or parameter sets produce identical internal states more easily than expected?

## Parser attacks

Can malicious encoded hashes cause:

* excessive allocation;
* integer overflow;
* long parsing times;
* exceptions;
* process termination?

## Side channels

Investigate:

* password-dependent memory access;
* timing leakage;
* cache leakage;
* shared-host risks.

ForgeHash version 1 intentionally uses password-dependent addressing. This may improve resistance to certain cracking hardware while increasing side-channel concerns.

It should not be used in environments where local attackers can observe fine-grained memory access unless this risk is accepted.

---

# 33. Recommended Default Profiles

## Interactive profile

```text
Memory:       65536 KiB
Iterations:   3
Parallelism:  1
Salt:         16 bytes
Output:       32 bytes
```

## Sensitive profile

```text
Memory:       262144 KiB
Iterations:   4
Parallelism:  2
Salt:         16 bytes
Output:       32 bytes
```

## Development profile

```text
Memory:       8192 KiB
Iterations:   1
Parallelism:  1
Salt:         16 bytes
Output:       32 bytes
```

The development profile must never be silently selected in production builds.

Profiles are recommendations, not security guarantees.

Applications should benchmark their own deployment hardware and target an acceptable verification duration.

---

# 34. Versioning Rules

## Version 1 stability

Once version 1 test vectors are published, the following must not change:

* input encoding;
* block size;
* seed derivation;
* expansion method;
* ForgeMix operation;
* round count;
* word schedule;
* permutation;
* reference selection;
* lane selection;
* slice rules;
* memory overwrite rules;
* finalization;
* encoded format.

Any incompatible change requires:

```text
v=2
```

## Implementation revisions

Bug fixes that preserve all test vectors may be released as library version updates without changing the ForgeHash algorithm version.

Example:

```text
Library version 1.2.0
Algorithm version 1
```

---

# 35. Documentation Requirements

The repository must include a prominent warning:

```text
ForgeHash is experimental cryptographic software.

It has not received sufficient independent review and must not be used
to protect production credentials or other sensitive data.
```

The README must explain:

* project purpose;
* algorithm status;
* build instructions;
* CLI usage;
* benchmark usage;
* encoded format;
* test vectors;
* known limitations;
* disclosure process.

---

# 36. Security Disclosure Policy

Create:

```text
SECURITY.md
```

It should request private reporting of:

* algorithmic weaknesses;
* memory bypasses;
* collisions;
* implementation vulnerabilities;
* side-channel problems;
* parser denial-of-service vulnerabilities;
* secret-memory exposure.

Do not offer financial rewards unless a real bounty program exists.

Do not claim security certification.

---

# 37. Implementation Milestones

## Milestone 1: Core primitives

Implement:

* parameter validation;
* little-endian encoding helpers;
* BLAKE3 adapter;
* Expand;
* FastRange;
* block XOR;
* block serialization;
* ForgeMix quarter round;
* ForgeMix round schedule.

Deliverable:

```text
ForgeMix unit tests
```

## Milestone 2: Single-lane memory filling

Implement:

* seed generation;
* block initialization;
* pass-zero filling;
* later-pass overwriting;
* finalization.

Use:

```text
parallelism = 1
```

Deliverable:

```text
Deterministic low-level hash output
```

## Milestone 3: Encoding and verification

Implement:

* secure salt generation;
* canonical encoder;
* strict parser;
* verification;
* fixed-time comparison;
* `NeedsRehash`.

Deliverable:

```text
End-to-end password hash API
```

## Milestone 4: Multiple lanes

Implement:

* lane memory division;
* four slices;
* cross-lane references;
* synchronization rules;
* deterministic sequential lane processing.

Deliverable:

```text
Multi-lane official test vectors
```

## Milestone 5: CLI and benchmarks

Implement:

* secure password prompt;
* hash command;
* verify command;
* benchmark command;
* vector command.

Deliverable:

```text
Runnable ForgeHash CLI
```

## Milestone 6: Parallel implementation

Implement:

* lane workers;
* slice barriers;
* deterministic output;
* sequential fallback.

Deliverable:

```text
Parallel output matches reference implementation
```

## Milestone 7: Analysis tooling

Implement:

* dependency recording;
* heat maps;
* avalanche tests;
* time-memory simulations;
* exported CSV and JSON results.

Deliverable:

```text
ForgeHash research report
```

---

# 38. Acceptance Criteria

ForgeHash version 1 is implementation-complete when:

* the algorithm is implemented from the specification;
* all official test vectors pass;
* parser limits are enforced;
* malformed hashes cannot trigger uncontrolled allocations;
* password verification uses fixed-time comparison;
* the sequential implementation is deterministic;
* the parallel implementation matches the sequential implementation;
* all supported parameter combinations produce stable output;
* the CLI can hash and verify passwords;
* benchmark results are reproducible;
* the complete memory region influences finalization;
* documentation clearly identifies the algorithm as experimental;
* no production-security claims are made.

---

# 39. Final Warning

ForgeHash should be treated as an experimental cryptographic engineering project.

A successful implementation demonstrates that the specification is internally consistent. It does not demonstrate that the algorithm is secure.

Security would require:

* public specification review;
* independent cryptanalysis;
* implementation audits;
* GPU and FPGA evaluation;
* time-memory trade-off analysis;
* side-channel analysis;
* years of practical scrutiny.

Until then, ForgeHash must remain clearly labeled as unsuitable for production password storage.
