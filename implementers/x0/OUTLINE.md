# ForgeHash-X v0 — condensed algorithm outline

Use with the full [`docs/forgehx/SPECIFICATION_X.md`](../../docs/forgehx/SPECIFICATION_X.md). Navigation aid only.

```text
1) encodedInput =
     LE32(0) || LE32(m) || LE32(t) || LE32(p) || LE32(outLen)
     || LE64(pwLen) || password
     || LE32(saltLen) || salt

2) seed = ForgeX.Hash("ForgeX/v0/seed", encodedInput)   # 32 bytes

3) for each lane:
     block[lane][0] = Expand(seed || LE32(lane) || LE32(0), 512)
     block[lane][1] = Expand(seed || LE32(lane) || LE32(1), 512)

4) for pass in 0..t-1:
     for slice in 0..3:
       for each lane:
         for blockIndex in slice range (start at 2 if pass==0 && slice==0):
           prev = previous block (wrap at 0)
           ref  = select reference (FastRange, slice rules, cross-lane if idx%16==0)
           combined = prev XOR ref
           if pass == 0:  store ForgeMix(combined, pass, lane, idx)
           else:          store old XOR ForgeMix(combined, pass, lane, idx)
       barrier

5) accumulator = XOR over lanes of blocks at last, 1/4, 1/2, 3/4

6) root = ForgeX.Hash(
     "ForgeX/v0/final" || seed || accumulator || … )

7) output = ForgeX.Xof("ForgeX/v0/output", root || …, outLen)

8) encoded = $forgehx$v=0$m=<m>,t=<t>,p=<p>$<salt-b64>$<hash-b64>
```

Toy profile for vectors 1–2: `m=1024`, `t=1`, `p=1`, `out=32`, salt 16 bytes.
Vector 3: same memory with `p=2`.
