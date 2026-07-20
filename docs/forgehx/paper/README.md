# ForgeHash-X research paper

**Experimental cryptography. Not a security proof. Not for production passwords.**

| Artifact | Path |
|----------|------|
| PDF | [`ForgeHash_X_Research_Paper.pdf`](ForgeHash_X_Research_Paper.pdf) |
| Source (Typst) | [`ForgeHash_X_Research_Paper.typ`](ForgeHash_X_Research_Paper.typ) |
| Bibliography | [`references.bib`](references.bib) |

## Rebuild

Requires [Typst](https://typst.app/) ≥ 0.13:

```bash
cd docs/forgehx/paper
typst compile ForgeHash_X_Research_Paper.typ ForgeHash_X_Research_Paper.pdf
```

## Contents (outline)

1. Introduction & related work  
2. Design goals / threat model  
3. ForgeX sponge & ForgePerm  
4. Memory-hard construction & encoding  
Full academic manuscript (Typst): abstract through ethics, plus extended comparative
discussion, cost sketch, worked example, reproducibility checklist, speculative
failure modes, pedagogy notes, versioning philosophy, and Appendices A–E.

Rebuild after edits; close any open PDF viewer if Windows locks the output file.
