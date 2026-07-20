"""Official ForgeHash-B3 v1 vectors — bit-identical across implementations."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from forgeh import Params, derive_hash, derive_seed, encode

VECTORS_DIR = (
    Path(__file__).resolve().parents[4] / "implementers" / "v1" / "vectors"
)


def _load_vectors():
    files = sorted(VECTORS_DIR.glob("*.json"))
    assert files, f"no vectors found in {VECTORS_DIR}"
    for path in files:
        data = json.loads(path.read_text(encoding="utf-8"))
        yield path.name, data


@pytest.mark.parametrize("name,vector", list(_load_vectors()))
def test_official_vector(name: str, vector: dict) -> None:
    password = bytes.fromhex(vector["passwordHex"])
    salt = bytes.fromhex(vector["saltHex"])
    params = Params(
        memory_kib=vector["memoryKiB"],
        iterations=vector["iterations"],
        parallelism=vector["parallelism"],
        output_length=vector["outputLength"],
        salt_length=len(salt),
    )

    seed = derive_seed(password, salt, params)
    assert seed.hex() == vector["seedHex"], f"seed mismatch for {name}"

    hash_ = derive_hash(password, salt, params)
    assert hash_.hex() == vector["hashHex"], f"hash mismatch for {name}"

    encoded = encode(1, params, salt, hash_)
    assert encoded == vector["encoded"], f"encoded mismatch for {name}"
