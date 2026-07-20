# ForgeHash-B3 v1 — condensed algorithm outline

Use with the full [`SPECIFICATION.md`](../../SPECIFICATION.md). This is a navigation aid, not a substitute.

```text
1) encodedInput =
     LE32(1) || LE32(m) || LE32(t) || LE32(p) || LE32(outLen)
     || LE64(pwLen) || password
     || LE32(saltLen) || salt
     || LE32(0)                 # empty context

2) seed = BLAKE3-DeriveKey("ForgeHash/v1/seed", encodedInput)   # 32 bytes

3) for each lane:
     block[lane][0] = Expand(seed || LE32(lane) || LE32(0), 1024)
     block[lane][1] = Expand(seed || LE32(lane) || LE32(1), 1024)

4) for pass in 0..t-1:
     for slice in 0..3:
       for each lane:
         for blockIndex in slice range (start at 2 if pass==0 && slice==0):
           prev = previous block (wrap at 0)
           ref  = select reference (FastRange, slice rules, cross-lane if idx%32==0)
           combined = prev XOR ref
           if pass == 0:  store ForgeMix(combined, pass, lane, idx)
           else:          store old XOR ForgeMix(combined, pass, lane, idx)
       barrier

5) accumulator = XOR over lanes of blocks at last, 1/4, 1/2, 3/4

6) for each 64-block group in flat memory order:
     groupDigest = BLAKE3("ForgeHash/v1/group" || LE64(i) || LE64(count) || blocks)

7) groupRoot = BLAKE3-incremental(
     "ForgeHash/v1/group-root",
     then for each i: LE64(i) || groupDigest[i]
   )

8) root = BLAKE3(
     "ForgeHash/v1/final" || seed || accumulator || groupRoot
     || LE32(m) || LE32(t) || LE32(p) || LE32(outLen)
   )

9) hash = BLAKE3-XOF("ForgeHash/v1/output" || root || seed, outLen)

10) encoded = $forgeh$v=1$m=m,t=t,p=p$b64(salt)$b64(hash)
```

`Expand(input, n) = BLAKE3-XOF("ForgeHash/v1/expand" || input, n)`
