# ForgeHash-X (Python)

**Experimental. Not for production. Not compatible with ForgeHash-B3.**

Pure-Python reference of ForgeHash-X v0 (`forgehx`) with a custom ForgeX sponge — no BLAKE3 dependency.

```bash
pip install forgehx --pre   # https://pypi.org/project/forgehx/
```

From this repo:

```bash
cd langs/python/forgehx
python -m pip install -e ".[dev]"
pytest -q
```

Vectors: `implementers/x0/vectors/`. Spec: `docs/forgehx/SPECIFICATION_X.md`.
