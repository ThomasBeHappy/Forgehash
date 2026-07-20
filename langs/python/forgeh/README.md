# forgeh (Python)

Experimental ForgeHash-B3 v1 reference in Python 3.10+.

**Not for production password storage.**

## Install / test

```bash
cd langs/python/forgeh
python -m pip install -e ".[dev]"
pytest -q
```

Depends on the [`blake3`](https://pypi.org/project/blake3/) package.

## Usage

```python
from forgeh import Params, hash_password, verify_password

encoded = hash_password("secret", Params.interactive())
ok = verify_password("secret", encoded)
```

Low-level:

```python
from forgeh import Params, derive_hash, derive_seed, encode

params = Params.development()  # 8 MiB — tests only
salt = bytes.fromhex("000102030405060708090a0b0c0d0e0f")
hash_ = derive_hash(b"password", salt, params)
encoded = encode(1, params, salt, hash_)
```

| Profile | Memory | Iterations | Lanes |
|---------|--------|------------|-------|
| `development()` | 8 MiB | 1 | 1 |
| `interactive()` | 64 MiB | 3 | 1 |
| `sensitive()` | 256 MiB | 4 | 2 |

Official vectors: `implementers/v1/vectors/`. Full suite can take several minutes; `pytest -q -k vector1` is a quick smoke check.
