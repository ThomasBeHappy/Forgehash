"""Pin ForgeHash-X v0 toy vectors from implementers/x0."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from forgehx import Params, derive_hash, derive_seed, encode, verify_password

VECTOR_DIR = Path(__file__).resolve().parents[4] / "implementers" / "x0" / "vectors"


def _load_vectors():
    files = sorted(VECTOR_DIR.glob("*.json"))
    assert files, f"no vectors in {VECTOR_DIR}"
    return files


@pytest.mark.parametrize("path", _load_vectors(), ids=lambda p: p.stem)
def test_vector(path: Path) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    password = bytes.fromhex(data["passwordHex"])
    salt = bytes.fromhex(data["saltHex"])
    params = Params(
        memory_kib=data["memoryKiB"],
        iterations=data["iterations"],
        parallelism=data["parallelism"],
        output_length=data["outputLength"],
        salt_length=len(salt),
    )
    seed = derive_seed(password, salt, params)
    digest = derive_hash(password, salt, params)
    encoded = encode(params, salt, digest)

    assert seed.hex() == data["seedHex"]
    assert digest.hex() == data["hashHex"]
    assert encoded == data["encoded"]
    assert verify_password(password, encoded)
