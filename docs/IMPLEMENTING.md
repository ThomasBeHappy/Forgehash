# Implementing ForgeHash-B3 in another language

Guide for writing a ForgeHash-B3 v1-compatible port.

Experimental cryptography. Matching test vectors does not make an implementation production-safe.

Existing packages (check these before starting a greenfield port): [`../langs/`](../langs/).

## Goal

Bit-identical digests for every official v1 vector in [`../implementers/v1/`](../implementers/v1/).

If all four match — and parallel lanes match sequential when you add threads — the port is v1-compatible.

## What you must implement

Follow [`../SPECIFICATION.md`](../SPECIFICATION.md) exactly. Do not substitute SHA-256, Argon2, or unkeyed BLAKE3 where derive-key/XOF/keyed-hash is required.

### Checklist

Use [`../implementers/v1/CHECKLIST.md`](../implementers/v1/CHECKLIST.md).

Minimum building blocks:

1. Little-endian helpers (`LE32`, `LE64`)
2. BLAKE3:
   - derive-key (`ForgeHash/v1/seed`)
   - XOF expand (`ForgeHash/v1/expand` ‖ input)
   - plain hash (group / final)
   - keyed hash (pepper helper only)
   - XOF output (`ForgeHash/v1/output` ‖ root ‖ seed)
3. `FastRange` = high 64 bits of 128-bit `x * n`
4. ForgeMix (8 rounds, row/column/diagonal, perm `73x+19 mod 128`, feed-forward)
5. Memory fill with 4 slices/pass, pass-0 init blocks 0–1, later-pass XOR overwrite
6. Reference selection + completed-slice cross-lane rules
7. Finalization: lane samples + 64-block groups + group-root + root + output XOF
8. Canonical encoding `$forgeh$v=1$m=...,t=...,p=...$salt$hash`

## Suggested port order

| Step | Deliverable | Validate with |
|------|-------------|----------------|
| A | BLAKE3 domain strings + seed | Vector seed hex |
| B | Expand + init blocks | Initialized block prefixes |
| C | ForgeMix only | Mix sample prefixes |
| D | Single-lane fill + finalize | Vector 1 & 2 final hash |
| E | Multi-lane + slices | Vector 3 & 4 |
| F | Encoder/parser | Encoded string exact match |
| G | Parallel lanes (optional) | Same digests as sequential |

## Official vectors

Machine-readable pack:

```text
implementers/v1/manifest.json
implementers/v1/vectors/*.json
```

Each vector JSON includes:

- password/salt hex
- parameters
- seed, group root, hash
- encoded string
- sampled init blocks, references, ForgeMix prefixes

Load `manifest.json`, then assert every listed field.

### Quick expected digests

| ID | Final hash (hex) |
|----|------------------|
| vector1 | `50aa2141813479be95d66a46efa0e076191addb64d1cf0a3cc832c6c9b54be4e` |
| vector2 | `02acdfa7faa0f149fe700b2f46b792fda8eaecd5f14844142c67709c561a6a98` |
| vector3 | `fc2f2e6bbcda6f7ca4a927d8a827b7224b30fcce829c8418005d7dacf6f061ba` |
| vector4 | `158230bd7d23be110989b6e9c26a408a0109c2f8ab95d5294e7558a7c9b40b3d` |

## Common pitfalls

- Using ordinary BLAKE3 hash instead of **derive-key** for the seed
- Forgetting domain prefixes (`ForgeHash/v1/...`)
- Big-endian word IO
- Accepting padded Base64 or reordered `m,t,p`
- Cross-lane reads from the current incomplete slice
- Finalizing from lane-last-blocks only (must include group digests)
- Unicode-normalizing passwords in the core
- Silently truncating long passwords

## Parallelism

`p` is lane count, not “use p OS threads”.

A multithreaded port must barrier after every slice and match the sequential digests exactly.

## Compatibility badge

An implementation may claim **ForgeHash-B3 v1 compatible** only if:

1. All official vectors pass bit-exactly
2. Parser rejects the non-canonical examples in the specification
3. Verification uses constant-time comparison
4. Docs state the algorithm is experimental / not production-ready

## Reference

- Spec: [`../SPECIFICATION.md`](../SPECIFICATION.md)
- .NET usage: [`USAGE.md`](USAGE.md)
- Research report: [`RESEARCH_REPORT.md`](RESEARCH_REPORT.md)
- .NET reference: `src/ForgeHash.Core/`
- Languages: [`../langs/README.md`](../langs/README.md)
- Docs site: [`../website/`](../website/) · https://thomasbehappy.github.io/Forgehash/
